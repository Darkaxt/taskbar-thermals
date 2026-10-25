using TaskbarThermals;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

if (args.FirstOrDefault() == "--live")
{
    using var stop = new CancellationTokenSource();
    var liveClient = new ThrmClient();
    var samples = Channel.CreateUnbounded<CoolerSnapshot>();
    liveClient.Changed += () => samples.Writer.TryWrite(liveClient.Current);
    var observing = liveClient.RunAsync(stop.Token);
    var readings = new List<CoolerSnapshot>();
    try
    {
        for (int i = 0; i < 3; i++)
        {
            var sample = await samples.Reader.ReadAsync();
            if (sample.State != CoolerState.Connected) throw new Exception($"Live THRM did not report the BS2 Pro: {sample.State}");
            readings.Add(sample);
        }
        string json = JsonSerializer.Serialize(readings, new JsonSerializerOptions { WriteIndented = true });
        if (args.Length > 1) File.WriteAllText(args[1], json);
        Console.WriteLine(json);
    }
    finally { stop.Cancel(); await observing; }
    return;
}

static void Equal<T>(T expected, T actual, string reason)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{reason}: expected {expected}, got {actual}");
}

const string connected = """{"isResponse":true,"success":true,"data":{"connected":true,"productId":"0x1002","model":"BS2PRO","currentData":{"currentRpm":3300,"targetRpm":4000}}}""";
var telemetry = new CoolerTelemetry();
telemetry.Accept(connected);
Equal<int?>(3300, telemetry.Current.Rpm, "Use measured RPM rather than commanded target");
Equal<int?>(83, telemetry.Current.Percent, "3300 of 4000 rounds to 83 percent");
telemetry.Accept("""{"isEvent":true,"type":"fan-data-update","data":{"currentRpm":0,"targetRpm":1700}}""");
Equal<int?>(0, telemetry.Current.Percent, "A stopped fan is a valid zero");
telemetry.Accept("""{"isEvent":true,"type":"device-disconnected","data":null}""");
Equal<int?>(null, telemetry.Current.Rpm, "Disconnect clears the last reading");
telemetry.Accept("""{"isEvent":true,"type":"fan-data-update","data":{"currentRpm":3300}}""");
Equal<int?>(null, telemetry.Current.Rpm, "Queued old readings cannot resurrect a disconnected device");
telemetry.Accept("""{"isEvent":true,"type":"device-connected","data":{"productId":"0x1002","model":"BS2PRO"}}""");
Equal<int?>(null, telemetry.Current.Rpm, "Reconnect waits for a new measurement");
telemetry.Accept("""{"isEvent":true,"type":"fan-data-update","data":{"currentRpm":4000}}""");
Equal<int?>(100, telemetry.Current.Percent, "A fresh frame restores the live reading");
telemetry.PipeClosed();
Equal<int?>(null, telemetry.Current.Rpm, "THRM exit clears the last reading");
telemetry.Accept("""{"isResponse":true,"success":true,"data":{"connected":false,"productId":"0x1002","currentData":{"currentRpm":3300}}}""");
Equal<int?>(null, telemetry.Current.Rpm, "A disconnected bootstrap ignores cached measurements");
telemetry.Accept("""{"isResponse":true,"success":true,"data":{"connected":true,"productId":"0x1004","currentData":{"currentRpm":3300}}}""");
Equal(CoolerState.DifferentModel, telemetry.Current.State, "Do not present another cooler as the user's BS2 Pro");
telemetry.Accept(connected);
foreach (var payload in new[] { "{}", "{\"currentRpm\":-1}", "{\"currentRpm\":65535}", "{\"currentRpm\":null}" })
{
    telemetry.Accept("{\"isEvent\":true,\"type\":\"fan-data-update\",\"data\":" + payload + "}");
    Equal<int?>(null, telemetry.Current.Rpm, "Missing or impossible measurements are unavailable");
}
telemetry.Accept(connected);
telemetry.Accept("""{"isEvent":true,"type":"heartbeat","data":{"currentRpm":100}}""");
Equal<int?>(3300, telemetry.Current.Rpm, "Unrelated event payloads cannot overwrite fan telemetry");
Console.WriteLine("PASS: measured speed, normalization, stopped fan, disconnect/reconnect, model identity and invalid-data contracts");

// Exercise the real asynchronous client against a local server using the inspected upstream wire format.
// Replacing GetDeviceStatus with a control request, blocking sensor work on pipe availability,
// retaining values on EOF, or losing cancellation must fail this contract.
string pipeName = "TaskbarThermals-contract-" + Guid.NewGuid().ToString("N");
using var cancellation = new CancellationTokenSource();
var client = new ThrmClient(pipeName);
var updates = Channel.CreateUnbounded<CoolerSnapshot>();
client.Changed += () => updates.Writer.TryWrite(client.Current);
using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
var running = client.RunAsync(cancellation.Token);
try
{
    for (int connection = 0; connection < 2; connection++)
    {
        await server.WaitForConnectionAsync();
        {
        using var reader = new StreamReader(server, Encoding.UTF8, false, 4096, leaveOpen: true);
        using var writer = new StreamWriter(server, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
        using var request = JsonDocument.Parse((await reader.ReadLineAsync())!);
        Equal("GetDeviceStatus", request.RootElement.GetProperty("type").GetString(), "Only the read-only status request may be sent");
        Equal("3.0", request.RootElement.GetProperty("protocolVersion").GetString(), "Use the upstream IPC protocol");
        var bootstrap = connected.Replace("\"isResponse\":true", "\"requestId\":\"" + request.RootElement.GetProperty("requestId").GetString() + "\",\"isResponse\":true");
        await writer.WriteLineAsync(bootstrap);
        Equal<int?>(83, (await updates.Reader.ReadAsync()).Percent, "Live pipe bootstrap reaches the UI snapshot");
        await writer.WriteLineAsync("""{"isEvent":true,"type":"device-disconnected"}""");
        Equal<int?>(null, (await updates.Reader.ReadAsync()).Rpm, "Live disconnect clears the UI snapshot");
        }
        server.Disconnect();
        Equal(CoolerState.WaitingForThrm, (await updates.Reader.ReadAsync()).State, "Server exit invalidates telemetry");
    }
}
finally
{
    cancellation.Cancel();
    await running;
}
using var noServerCancellation = new CancellationTokenSource();
var waitingClient = new ThrmClient("TaskbarThermals-absent-" + Guid.NewGuid().ToString("N"));
var waiting = waitingClient.RunAsync(noServerCancellation.Token);
Equal<int?>(null, waitingClient.Current.Rpm, "Absent THRM never supplies a false zero");
noServerCancellation.Cancel();
await waiting;
Console.WriteLine("PASS: actual named-pipe request boundary, streamed updates, server restart and cancellation with THRM absent");
OverlaySurfaceContract.Run();
Console.WriteLine("PASS: actual overlay background, transparent margins and rounded corners at each DPI/theme");
SensorFreshnessContract.Run();
