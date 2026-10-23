using System.Text.Json;

namespace TaskbarThermals;

internal enum CoolerState { WaitingForThrm, Disconnected, WaitingForReading, Connected, DifferentModel, InvalidReading }

internal sealed record CoolerSnapshot(CoolerState State, int? Rpm = null, DateTimeOffset? UpdatedAt = null)
{
    public const int MaximumRpm = 4000;
    public int? Percent => Rpm is int rpm ? (int)Math.Round(rpm * 100.0 / MaximumRpm, MidpointRounding.AwayFromZero) : null;
    public static readonly CoolerSnapshot Waiting = new(CoolerState.WaitingForThrm);
}

internal sealed class CoolerTelemetry
{
    private bool _connected, _supported;
    public CoolerSnapshot Current { get; private set; } = CoolerSnapshot.Waiting;

    public CoolerSnapshot Accept(string frame)
    {
        using var document = JsonDocument.Parse(frame);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return Current;
        if (IsTrue(root, "isResponse"))
        {
            if (!IsTrue(root, "success") || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return Current;
            if (!data.TryGetProperty("connected", out _)) return Current;
            _connected = IsTrue(data, "connected");
            _supported = IsBs2Pro(data);
            Current = new(_connected ? (_supported ? CoolerState.WaitingForReading : CoolerState.DifferentModel) : CoolerState.Disconnected);
            if (_connected && _supported && data.TryGetProperty("currentData", out var fan)) ReadFan(fan);
        }
        else if (IsTrue(root, "isEvent") && root.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
        {
            switch (type.GetString())
            {
                case "device-disconnected":
                case "device-error":
                    _connected = false;
                    Current = new(CoolerState.Disconnected);
                    break;
                case "device-connected":
                    _connected = true;
                    _supported = root.TryGetProperty("data", out var device) && IsBs2Pro(device);
                    Current = new(_supported ? CoolerState.WaitingForReading : CoolerState.DifferentModel);
                    break;
                case "fan-data-update" when _connected && _supported:
                    if (root.TryGetProperty("data", out var fan)) ReadFan(fan);
                    else Current = new(CoolerState.InvalidReading);
                    break;
            }
        }
        return Current;
    }

    public CoolerSnapshot PipeClosed()
    {
        _connected = _supported = false;
        return Current = CoolerSnapshot.Waiting;
    }

    private void ReadFan(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("currentRpm", out var value)
            && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int rpm) && rpm is >= 0 and <= CoolerSnapshot.MaximumRpm)
            Current = new(CoolerState.Connected, rpm, DateTimeOffset.UtcNow);
        else Current = new(CoolerState.InvalidReading);
    }

    private static bool IsBs2Pro(JsonElement data) => data.ValueKind == JsonValueKind.Object
        && data.TryGetProperty("productId", out var id) && id.ValueKind == JsonValueKind.String
        && string.Equals(id.GetString(), "0x1002", StringComparison.OrdinalIgnoreCase);

    private static bool IsTrue(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
