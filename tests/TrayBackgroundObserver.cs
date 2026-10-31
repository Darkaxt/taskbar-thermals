using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using TaskbarThermals;

/// <summary>Opt-in read-only desktop observation; absent from the released application.</summary>
internal static class TrayBackgroundObserver
{
    public static void Run(string output)
    {
        var observations = new List<object>();
        using var ready = new ManualResetEventSlim();
        Action? finish = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                using var context = new ApplicationContext();
                using var dispatcher = new Control();
                _ = dispatcher.Handle;
                finish = () => dispatcher.BeginInvoke(new Action(context.ExitThread));
                using var timer = new System.Windows.Forms.Timer { Interval = 100 };
                TaskbarSurface? surface = null;
                (bool? accepted, bool overflow, int? color) last = (null, false, null);
                long origin = Stopwatch.GetTimestamp();
                timer.Tick += (_, _) =>
                {
                    IntPtr taskbar = Native.FindWindow("Shell_TrayWnd", null);
                    int dpi = (int)Native.GetDpiForWindow(taskbar);
                    bool accepted = Native.TryReadTrayBackground(taskbar, dpi, out var tint);
                    bool overflow = Native.TrayOverflowVisible;
                    int? previous = surface?.Color.ToArgb();
                    surface = TaskbarSurface.Read(taskbar, dpi, Theme.SystemUsesLightTheme(), surface);
                    int? color = surface?.Color.ToArgb();
                    if ((accepted, overflow, color) == last) return;
                    last = (accepted, overflow, color);
                    Native.GetCursorPos(out var cursor);
                    var row = new
                    {
                        elapsedMs = Stopwatch.GetElapsedTime(origin).TotalMilliseconds,
                        accepted, overflow, cursor = new { cursor.X, cursor.Y }, dpi,
                        sampledColor = accepted ? (int?)tint.ToArgb() : null,
                        previousColor = previous, surfaceColor = color,
                    };
                    if (observations.Count < 1000) observations.Add(row);
                    Console.WriteLine(JsonSerializer.Serialize(row));
                };
                timer.Start();
                ready.Set();
                Application.Run(context);
            }
            catch (Exception ex) { failure = ex; ready.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        if (failure is null)
        {
            Console.WriteLine("READY: observe normal tray, arrow hover, open overflow with pointer moved away, then close it. Press Enter here to finish.");
            Console.ReadLine();
            finish!();
        }
        thread.Join();
        if (failure is not null) throw failure;
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            assembly = typeof(Native).Assembly.FullName,
            executable = Environment.ProcessPath,
            observationOnly = true,
            observationLimit = 1000,
            observations,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
