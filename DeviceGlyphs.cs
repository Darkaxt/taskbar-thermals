using System.Drawing.Drawing2D;

namespace TaskbarThermals;

/// <summary>Small monochrome device marks, drawn at the actual taskbar DPI.</summary>
internal static class DeviceGlyphs
{
    public static bool HasGlyph(Device device) => device is Device.Cpu or Device.Gpu or Device.Cooler;

    public static void Draw(Graphics graphics, Device device, RectangleF bounds, Color color)
    {
        var saved = graphics.Save();
        graphics.TranslateTransform(bounds.X, bounds.Y);
        graphics.ScaleTransform(bounds.Width / 16f, bounds.Height / 16f);
        using var pen = new Pen(color, 1.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);
        if (device == Device.Cpu)
        {
            graphics.DrawRectangle(pen, 3.5f, 3.5f, 9, 9);
            graphics.DrawRectangle(pen, 6, 6, 4, 4);
            foreach (float p in new[] { 5f, 8f, 11f })
            {
                graphics.DrawLine(pen, p, 1, p, 3.5f); graphics.DrawLine(pen, p, 12.5f, p, 15);
                graphics.DrawLine(pen, 1, p, 3.5f, p); graphics.DrawLine(pen, 12.5f, p, 15, p);
            }
        }
        else if (device == Device.Gpu)
        {
            graphics.DrawRectangle(pen, 2, 4, 12, 8);
            graphics.DrawLine(pen, 1, 2, 1, 14);
            graphics.DrawLine(pen, 4, 12, 4, 14);
            graphics.DrawLine(pen, 6, 12, 6, 14);
            graphics.DrawEllipse(pen, 6, 5.8f, 4.4f, 4.4f);
            graphics.DrawLine(pen, 12, 6, 12, 10);
        }
        else if (device == Device.Cooler)
        {
            graphics.TranslateTransform(8, 8);
            for (int blade = 0; blade < 3; blade++)
            {
                using var path = new GraphicsPath();
                path.AddBezier(1.3f, -0.6f, 7, -7, -0.5f, -9, -1.2f, -3);
                path.AddBezier(-1.2f, -3, -1.5f, -1, -0.4f, -0.5f, 1.3f, -0.6f);
                graphics.FillPath(brush, path);
                graphics.RotateTransform(120);
            }
            graphics.FillEllipse(brush, -1.5f, -1.5f, 3, 3);
        }
        graphics.Restore(saved);
    }
}
