using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RebelDesk.Server.Data;

namespace RebelDesk.Server.Hubs;

/// <summary>
/// Signaling only. Never receives media, never forwards media. Carries:
/// - agent presence (a device connects and joins group "device:{deviceId}")
/// - session lifecycle events (request, approve, reject, end)
/// - SDP offer / answer / ICE candidates between the browser viewer and the agent
/// The hub authorises every message against the session record in the database.
/// </summary>
[Authorize]
public class SignalingHub : Hub
{
    private static readonly ConcurrentDictionary<Guid, string> _agentConnections = new();
    private static readonly ConcurrentDictionary<string, Guid> _connectionToDevice = new();
    private static readonly ConcurrentDictionary<Guid, HashSet<string>> _sessionViewers = new();
    private static readonly ConcurrentDictionary<string, Guid> _connectionToSession = new();

    private readonly AppDbContext _db;
    private readonly ILogger<SignalingHub> _log;

    public SignalingHub(AppDbContext db, ILogger<SignalingHub> log)
    {
        _db = db;
        _log = log;
    }

    private string Kind => Context.User?.FindFirst("kind")?.Value ?? "";
    private Guid Sub => Guid.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                    ?? Context.User!.FindFirst("sub")!.Value);

    public override async Task OnConnectedAsync()
    {
        if (Kind == "agent")
        {
            var deviceId = Sub;
            _agentConnections[deviceId] = Context.ConnectionId;
            _connectionToDevice[Context.ConnectionId] = deviceId;

            var device = await _db.Devices.FindAsync(deviceId);
            if (device != null)
            {
                device.LastSeenUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            _log.LogInformation("Agent connected: device {DeviceId}", deviceId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectionToDevice.TryRemove(Context.ConnectionId, out var deviceId))
        {
            _agentConnections.TryRemove(deviceId, out _);
            var active = await _db.Sessions
                .Where(s => s.DeviceId == deviceId
                            && (s.Status == SessionStatus.Approved || s.Status == SessionStatus.Active))
                .ToListAsync();
            foreach (var s in active)
            {
                s.Status = SessionStatus.Ended;
                s.EndedAtUtc = DateTime.UtcNow;
                s.EndReason = "agent-disconnected";
                await NotifySessionParticipants(s.Id, "sessionEnded", new { reason = "agent-disconnected" });
            }
            await _db.SaveChangesAsync();
            _log.LogInformation("Agent disconnected: device {DeviceId}", deviceId);
        }

        if (_connectionToSession.TryRemove(Context.ConnectionId, out var sessionId))
        {
            if (_sessionViewers.TryGetValue(sessionId, out var viewers))
            {
                lock (viewers) { viewers.Remove(Context.ConnectionId); }
            }
            var session = await _db.Sessions.FindAsync(sessionId);
            if (session != null && (session.Status == SessionStatus.Approved || session.Status == SessionStatus.Active))
            {
                session.Status = SessionStatus.Ended;
                session.EndedAtUtc = DateTime.UtcNow;
                session.EndReason = "viewer-disconnected";
                await _db.SaveChangesAsync();
                await NotifyAgent(session.DeviceId, "sessionEnded", new { sessionId, reason = "viewer-disconnected" });
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public static bool IsAgentOnline(Guid deviceId) => _agentConnections.ContainsKey(deviceId);

    // ---- viewer -> hub ----

    public async Task JoinSession(Guid sessionId)
    {
        var session = await _db.Sessions.Include(s => s.Device).FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) throw new HubException("session-not-found");
        if (Kind != "user" || session.InitiatorUserId != Sub) throw new HubException("forbidden");
        if (session.Status == SessionStatus.Ended || session.Status == SessionStatus.Rejected
            || session.Status == SessionStatus.Expired)
            throw new HubException("session-not-active");

        _connectionToSession[Context.ConnectionId] = sessionId;
        var set = _sessionViewers.GetOrAdd(sessionId, _ => new HashSet<string>());
        lock (set) { set.Add(Context.ConnectionId); }

        await NotifyAgent(session.DeviceId, "sessionRequested", new
        {
            sessionId,
            initiatorUserId = session.InitiatorUserId,
            initiatorIp = session.InitiatorIp,
            createdAtUtc = session.CreatedAtUtc,
        });
    }

    public async Task ApproveSession(Guid sessionId)
    {
        var session = await _db.Sessions.Include(s => s.Device).FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) throw new HubException("session-not-found");
        if (Kind != "agent" || session.DeviceId != Sub) throw new HubException("forbidden");
        if (session.Status != SessionStatus.Requested) throw new HubException("session-not-requestable");

        session.Status = SessionStatus.Approved;
        session.ApprovedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await NotifySessionParticipants(sessionId, "sessionApproved", new { sessionId });
    }

    public async Task RejectSession(Guid sessionId, string? reason)
    {
        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) throw new HubException("session-not-found");
        if (Kind != "agent" || session.DeviceId != Sub) throw new HubException("forbidden");
        if (session.Status != SessionStatus.Requested) throw new HubException("session-not-requestable");

        session.Status = SessionStatus.Rejected;
        session.EndedAtUtc = DateTime.UtcNow;
        session.EndReason = string.IsNullOrWhiteSpace(reason) ? "rejected-by-host" : $"rejected: {reason}";
        await _db.SaveChangesAsync();

        await NotifySessionParticipants(sessionId, "sessionRejected", new { sessionId, reason });
    }

    public async Task EndSession(Guid sessionId, string? reason)
    {
        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) throw new HubException("session-not-found");
        var isAgent = Kind == "agent" && session.DeviceId == Sub;
        var isUser = Kind == "user" && session.InitiatorUserId == Sub;
        if (!isAgent && !isUser) throw new HubException("forbidden");

        if (session.Status is SessionStatus.Ended or SessionStatus.Rejected) return;

        session.Status = SessionStatus.Ended;
        session.EndedAtUtc = DateTime.UtcNow;
        session.EndReason = string.IsNullOrWhiteSpace(reason)
            ? (isAgent ? "ended-by-host" : "ended-by-viewer")
            : reason;
        await _db.SaveChangesAsync();

        await NotifySessionParticipants(sessionId, "sessionEnded", new { sessionId, reason = session.EndReason });
    }

    public async Task Signal(Guid sessionId, SignalingPayload payload)
    {
        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) throw new HubException("session-not-found");
        var isAgent = Kind == "agent" && session.DeviceId == Sub;
        var isUser = Kind == "user" && session.InitiatorUserId == Sub;
        if (!isAgent && !isUser) throw new HubException("forbidden");
        if (session.Status is SessionStatus.Ended or SessionStatus.Rejected or SessionStatus.Expired)
            throw new HubException("session-not-active");

        if (isAgent)
        {
            await SendToViewers(sessionId, "signal", payload);
        }
        else
        {
            if (session.Status == SessionStatus.Approved) session.Status = SessionStatus.Active;
            await _db.SaveChangesAsync();
            await NotifyAgent(session.DeviceId, "signal", new { sessionId, payload });
        }
    }

    // ---- helpers ----

    private async Task NotifyAgent(Guid deviceId, string method, object arg)
    {
        if (_agentConnections.TryGetValue(deviceId, out var connId))
            await Clients.Client(connId).SendAsync(method, arg);
    }

    private async Task SendToViewers(Guid sessionId, string method, object arg)
    {
        if (_sessionViewers.TryGetValue(sessionId, out var viewers))
        {
            string[] ids;
            lock (viewers) { ids = viewers.ToArray(); }
            foreach (var id in ids)
                await Clients.Client(id).SendAsync(method, arg);
        }
    }

    private async Task NotifySessionParticipants(Guid sessionId, string method, object arg)
    {
        await SendToViewers(sessionId, method, arg);
        var session = await _db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session != null) await NotifyAgent(session.DeviceId, method, arg);
    }
}

public record SignalingPayload(string Type, string? Sdp, string? Candidate, string? SdpMid, int? SdpMLineIndex);
