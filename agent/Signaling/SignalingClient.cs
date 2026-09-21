using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using RebelDesk.Agent.WebRtc;

namespace RebelDesk.Agent.Signaling;

public sealed class SessionRequestedEvent
{
    public Guid SessionId { get; init; }
    public string? InitiatorUserId { get; init; }
    public string? InitiatorIp { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class SignalingClient : IAsyncDisposable
{
    private readonly HubConnection _conn;
    private readonly ILogger<SignalingClient> _log;

    public event Func<SessionRequestedEvent, Task>? SessionRequested;
    public event Func<Guid, Task>? SessionApproved;
    public event Func<Guid, string?, Task>? SessionEnded;
    public event Func<Guid, SignalingMessage, Task>? Signal;

    public SignalingClient(string serverUrl, string agentToken, ILogger<SignalingClient> log)
    {
        _log = log;
        _conn = new HubConnectionBuilder()
            .WithUrl($"{serverUrl.TrimEnd('/')}/hub/signaling",
                o => { o.AccessTokenProvider = () => Task.FromResult<string?>(agentToken); })
            .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2),
                                            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        _conn.On<JsonElement>("sessionRequested", async payload =>
        {
            var evt = new SessionRequestedEvent
            {
                SessionId = payload.GetProperty("sessionId").GetGuid(),
                InitiatorUserId = payload.TryGetProperty("initiatorUserId", out var iu) ? iu.GetString() : null,
                InitiatorIp = payload.TryGetProperty("initiatorIp", out var ip) ? ip.GetString() : null,
                CreatedAtUtc = payload.TryGetProperty("createdAtUtc", out var t) ? t.GetDateTime() : DateTime.UtcNow,
            };
            if (SessionRequested is not null) await SessionRequested(evt);
        });

        _conn.On<JsonElement>("sessionApproved", async payload =>
        {
            var id = payload.GetProperty("sessionId").GetGuid();
            if (SessionApproved is not null) await SessionApproved(id);
        });

        _conn.On<JsonElement>("sessionEnded", async payload =>
        {
            var id = payload.TryGetProperty("sessionId", out var s) ? s.GetGuid() : Guid.Empty;
            var reason = payload.TryGetProperty("reason", out var r) ? r.GetString() : null;
            if (SessionEnded is not null) await SessionEnded(id, reason);
        });

        _conn.On<JsonElement>("signal", async payload =>
        {
            var sessionId = payload.GetProperty("sessionId").GetGuid();
            var p = payload.GetProperty("payload");
            var msg = new SignalingMessage(
                p.GetProperty("type").GetString() ?? "",
                p.TryGetProperty("sdp", out var sdp) ? sdp.GetString() : null,
                p.TryGetProperty("candidate", out var c) ? c.GetString() : null,
                p.TryGetProperty("sdpMid", out var mid) ? mid.GetString() : null,
                p.TryGetProperty("sdpMLineIndex", out var idx) && idx.ValueKind == JsonValueKind.Number ? idx.GetInt32() : null);
            if (Signal is not null) await Signal(sessionId, msg);
        });

        _conn.Reconnecting += err => { _log.LogWarning(err, "SignalR reconnecting"); return Task.CompletedTask; };
        _conn.Reconnected += id => { _log.LogInformation("SignalR reconnected: {Id}", id); return Task.CompletedTask; };
        _conn.Closed += err => { _log.LogWarning(err, "SignalR closed"); return Task.CompletedTask; };
    }

    public Task StartAsync(CancellationToken ct) => _conn.StartAsync(ct);

    public Task ApproveAsync(Guid sessionId) => _conn.InvokeAsync("ApproveSession", sessionId);
    public Task RejectAsync(Guid sessionId, string reason) => _conn.InvokeAsync("RejectSession", sessionId, reason);
    public Task EndAsync(Guid sessionId, string reason) => _conn.InvokeAsync("EndSession", sessionId, reason);
    public Task SignalAsync(Guid sessionId, SignalingMessage msg) =>
        _conn.InvokeAsync("Signal", sessionId, msg);

    public async ValueTask DisposeAsync() => await _conn.DisposeAsync();
}
