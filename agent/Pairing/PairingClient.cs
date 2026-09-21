using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using RebelDesk.Agent.Config;

namespace RebelDesk.Agent.Pairing;

public class PairingClient
{
    private readonly HttpClient _http;
    private readonly ILogger<PairingClient> _log;
    public PairingClient(HttpClient http, ILogger<PairingClient> log) { _http = http; _log = log; }

    private record ConsumeReq(string Code, string DeviceName, string OsPlatform, string PublicKey);
    private record ConsumeResp(Guid DeviceId, string AgentToken, string RefreshToken);
    private record RefreshReq(Guid DeviceId, string RefreshToken);

    public async Task<DeviceState> PairAsync(string code, string deviceName, CancellationToken ct)
    {
        using var ed = new ECDsa(); // placeholder handle
        // Ed25519 not directly in netstandard: use RSA as identity for v0.1; upgrade to Ed25519 via BouncyCastle later.
        using var rsa = RSA.Create(2048);
        var pubKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        var privKey = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());

        var body = new ConsumeReq(code.Trim(), deviceName, "Windows", pubKey);
        var res = await _http.PostAsJsonAsync("/api/pairing/consume", body, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _log.LogError("Pairing consume failed {Status}: {Body}", res.StatusCode, err);
            throw new InvalidOperationException($"Pairing failed: {(int)res.StatusCode} {err}");
        }
        var parsed = await res.Content.ReadFromJsonAsync<ConsumeResp>(ct)
                     ?? throw new InvalidOperationException("Empty pairing response.");
        return new DeviceState
        {
            DeviceId = parsed.DeviceId,
            DeviceName = deviceName,
            AgentToken = parsed.AgentToken,
            RefreshToken = parsed.RefreshToken,
            OsPlatform = "Windows",
            PublicKey = pubKey,
            PrivateKey = privKey,
        };
    }

    public async Task<(string AgentToken, string RefreshToken)?> RefreshAsync(DeviceState state, CancellationToken ct)
    {
        var body = new RefreshReq(state.DeviceId, state.RefreshToken);
        var res = await _http.PostAsJsonAsync("/api/pairing/refresh", body, ct);
        if (!res.IsSuccessStatusCode)
        {
            _log.LogWarning("Refresh failed {Status}", res.StatusCode);
            return null;
        }
        var parsed = await res.Content.ReadFromJsonAsync<ConsumeResp>(ct);
        return parsed is null ? null : (parsed.AgentToken, parsed.RefreshToken);
    }
}

internal class ECDsa : IDisposable { public void Dispose() { } }
