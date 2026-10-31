using System.Drawing;
using TaskbarThermals;

internal static class OverlaySurfaceContract
{
    public static void Run()
    {
        var tray = new Rectangle(4016, 2088, 1104, 72);
        foreach (var point in new[] { new Point(4022, 2106), new Point(4034, 2124), new Point(4800, 2124) })
            if (Native.CanSampleTrayBackground(tray, point, overflowVisible: false))
                throw new Exception("Tray hover/pressed backgrounds must not become the overlay's normal tint.");
        if (Native.CanSampleTrayBackground(tray, new Point(3500, 2000), overflowVisible: true))
            throw new Exception("Open overflow must retain the tint even after the pointer leaves the tray.");
        if (!Native.CanSampleTrayBackground(tray, new Point(3500, 2000), overflowVisible: false))
            throw new Exception("Normal tint sampling must resume when tray interaction ends.");
        if (Native.CanSampleTrayBackground(Rectangle.Empty, Point.Empty, overflowVisible: false))
            throw new Exception("An unavailable tray cannot supply a normal tint.");
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var overlay = new OverlayForm();
                foreach (int dpi in new[] { 96, 144, 192 })
                foreach (bool light in new[] { false, true })
                {
                    using var bitmap = overlay.SnapshotOverlay(Reading.Empty, dpi, light);
                    var expected = light ? Theme.LightMode.Surface : Theme.Dark.Surface;
                    var background = bitmap.GetPixel((int)(3 * dpi / 96f), bitmap.Height / 2);
                    if (background.ToArgb() != expected.ToArgb())
                        throw new Exception($"Normal overlay surface missing at {dpi} DPI / light={light}: {background}");
                    if (bitmap.GetPixel(0, 0).A > 1 || bitmap.GetPixel(bitmap.Width / 2, 0).A > 1)
                        throw new Exception("Unused taskbar margin must remain transparent.");
                    int inset = (int)(4 * dpi / 96f);
                    if (bitmap.GetPixel((int)(1 * dpi / 96f), inset).A > 10)
                        throw new Exception("The background must have rounded corners.");
                }
                var trayTint = Color.FromArgb(92, 113, 114);
                using var tinted = overlay.SnapshotOverlay(Reading.Empty, 144, false, trayTint);
                if (tinted.GetPixel(5, tinted.Height / 2).ToArgb() != trayTint.ToArgb())
                    throw new Exception("The rendered background must use the current native tray tint.");
                using var ordinary = overlay.SnapshotOverlay(Reading.Empty, 144, false, segmented: false);
                if (ordinary.GetPixel(5, ordinary.Height / 2).A != 1)
                    throw new Exception("The ordinary taskbar overlay must remain transparent.");
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }
}
