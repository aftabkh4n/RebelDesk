using System.Text.Json;

namespace RebelDesk.Agent.Config;

public sealed record AgentSettings
{
    public string ServerUrl { get; init; } = "http://localhost:8080";
    public string DeviceStateFile { get; init; } = "device.json";
    public int FrameRate { get; init; } = 15;
    public int MaxWidth { get; init; } = 1920;
    public int MaxHeight { get; init; } = 1080;
    public string LogLevel { get; init; } = "Information";
}

public sealed class DeviceState
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = default!;
    public string AgentToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public string OsPlatform { get; set; } = "Windows";
    public string PublicKey { get; set; } = default!;
    public string PrivateKey { get; set; } = default!;
    public DateTime PairedAtUtc { get; set; } = DateTime.UtcNow;

    public static DeviceState? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<DeviceState>(File.ReadAllText(path)); }
        catch { return null; }
    }

    public void Save(string path)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(this, opts));
    }
}
