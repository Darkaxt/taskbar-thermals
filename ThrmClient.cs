using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace TaskbarThermals;

/// <summary>Observes THRM's v3 IPC. Never opens HID or sends cooler-control requests.</summary>
internal sealed class ThrmClient
{
    private readonly string _pipeName;
    private CoolerSnapshot _current = CoolerSnapshot.Waiting;
    public CoolerSnapshot Current => Volatile.Read(ref _current);
    public event Action? Changed;

    public ThrmClient(string pipeName = "THRM-IPC") => _pipeName = pipeName;

    public async Task RunAsync(CancellationToken cancellation)
    {
        var telemetry = new CoolerTelemetry();
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                // The framework waits for pipe availability. Cancellation belongs to our application lifecycle.
                await pipe.ConnectAsync(cancellation).ConfigureAwait(false);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
                string requestId = Guid.NewGuid().ToString("N");
                await writer.WriteLineAsync(JsonSerializer.Serialize(new { protocolVersion = "3.0", requestId, type = "GetDeviceStatus" }).AsMemory(), cancellation).ConfigureAwait(false);
                // v3.7.3 subscribes new clients by default. Fan/model/disconnect events follow the snapshot.
                while (await reader.ReadLineAsync(cancellation).ConfigureAwait(false) is string frame)
                {
                    Publish(telemetry.Accept(frame));
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { break; }
            catch (IOException) { /* EOF/broken pipe: invalidate and wait for the next THRM instance. */ }
            catch (JsonException) { /* A corrupt stream needs a new bootstrap snapshot, never a cached reading. */ }
            catch (UnauthorizedAccessException)
            {
                Publish(telemetry.PipeClosed());
                return; // A permission failure is not a reason to spin or change device permissions.
            }
            finally { Publish(telemetry.PipeClosed()); }
        }
    }

    private void Publish(CoolerSnapshot snapshot)
    {
        if (snapshot == Current) return;
        Volatile.Write(ref _current, snapshot);
        Changed?.Invoke();
    }
}
