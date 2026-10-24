using Microsoft.Win32;

namespace TaskbarThermals;

/// <summary>The compact surface used beside StartAllBack's separated tray segment.</summary>
internal sealed record TaskbarSurface(Color Color)
{
    public RectangleF Bounds(Size size, int dpi)
    {
        float inset = 4f * dpi / 96f;
        return new RectangleF(0, inset, size.Width, Math.Max(1, size.Height - 2 * inset));
    }

    public float Radius(int dpi) => 4f * dpi / 96f;

    public static TaskbarSurface? Read(IntPtr taskbar, int dpi, bool light, TaskbarSurface? previous)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\StartIsBack");
        if (key?.GetValue("LegacyTaskbar") is not int legacy || legacy != 1 ||
            key.GetValue("TaskbarOneSegment") is not int segmented || segmented != 0)
            return null; // The ordinary taskbar already supplies the background beneath our text.

        return Native.TryReadTrayBackground(taskbar, dpi, out var color)
            ? new TaskbarSurface(color)
            : previous ?? new TaskbarSurface(light ? Theme.LightMode.Surface : Theme.Dark.Surface);
    }
}
