using System.Diagnostics;
using System.Xml.Linq;
using TaskbarThermals;

internal static class SensorFreshnessContract
{
    public static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var overlay = new OverlayForm(previewOnly: true);
                long sampled = Stopwatch.GetTimestamp();
                overlay.OnReading(new Reading { CpuTemp = 91, CpuLoad = 39, GpuTemp = 57, GpuLoad = 0 }, DateTime.Now, sampled);
                overlay.OnCoolerReading(new(CoolerState.Connected, 3400));
                overlay.RefreshSensorFreshness(sampled + 2 * Stopwatch.Frequency);
                if (overlay.DisplayedReading.CpuTemp != 91)
                    throw new Exception("Normal sampling cadence must retain its current reading.");
                overlay.RefreshSensorFreshness(sampled + 4 * Stopwatch.Frequency);
                if (overlay.DisplayedReading.CpuTemp is not null || overlay.DisplayedReading.CpuLoad is not null
                    || overlay.DisplayedReading.GpuTemp is not null || overlay.DisplayedReading.GpuLoad is not null)
                    throw new Exception("A stalled sampler must not leave the previous CPU/GPU readings on the overlay.");
                if (overlay.DisplayedReading.Cooler.Percent != 85 || !overlay.AccessibleDescription!.Contains("paused"))
                    throw new Exception("Stale sensors must retain independent fan telemetry and explain the pause.");
                overlay.OnCoolerReading(new(CoolerState.Connected, 3500));
                if (overlay.DisplayedReading.Cooler.Percent != 88 || overlay.DisplayedReading.CpuTemp is not null)
                    throw new Exception("New fan telemetry must continue while CPU/GPU sampling is paused.");
                overlay.OnReading(new Reading { CpuTemp = 76, CpuLoad = 21, GpuTemp = 59, GpuLoad = 12 }, DateTime.Now, Stopwatch.GetTimestamp());
                if (overlay.DisplayedReading.CpuTemp != 76 || overlay.DisplayedReading.GpuLoad != 12
                    || overlay.AccessibleDescription!.Contains("paused"))
                    throw new Exception("A completed new sample must restore the live display.");
                overlay.OnReading(new Reading { CpuTemp = 99, GpuTemp = 99 }, DateTime.Now, Stopwatch.GetTimestamp() - 4 * Stopwatch.Frequency);
                if (overlay.DisplayedReading.CpuTemp is not null)
                    throw new Exception("A sample delayed in the UI queue must not be presented as fresh.");
                overlay.OnReading(new Reading { CpuTemp = 77 }, DateTime.Now, Stopwatch.GetTimestamp());
                overlay.OnSensorReadFailed();
                if (overlay.DisplayedReading.CpuTemp is not null || !overlay.AccessibleDescription!.Contains("unavailable"))
                    throw new Exception("A read failure must invalidate the old sensor values immediately.");
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;

        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var xml = XDocument.Parse(Startup.BuildTaskXml(@"D:\Tools\Taskbar Thermals\1.2.2\TaskbarThermals.exe", @"TEST\Owner", @"D:\Tools\Taskbar Thermals\runtime"));
        string Value(string name) => xml.Descendants(ns + name).Single().Value;
        if (Value("Arguments") != "--runtime-directory \"D:\\Tools\\Taskbar Thermals\\runtime\""
            || Value("MultipleInstancesPolicy") != "IgnoreNew" || Value("ExecutionTimeLimit") != "PT0S"
            || Value("RunLevel") != "HighestAvailable" || Value("LogonType") != "InteractiveToken"
            || xml.Descendants(ns + "Delay").Any())
            throw new Exception("Startup must preserve the persistent runtime, collision policy and event-driven login trigger.");
        Console.WriteLine("PASS: actual overlay stale/failure/recovery behavior and native startup action contract");
    }
}
