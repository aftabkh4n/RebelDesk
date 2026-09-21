using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RebelDesk.Server.Auth;
using RebelDesk.Server.Data;
using RebelDesk.Server.Hubs;
using RebelDesk.Server.Models;

namespace RebelDesk.Server.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly TurnCredentialService _turn;

    public SessionsController(AppDbContext db, JwtTokenService jwt, TurnCredentialService turn)
    {
        _db = db;
        _jwt = jwt;
        _turn = turn;
    }

    private Guid RequireUserId()
    {
        if (User.FindFirst("kind")?.Value != "user")
            throw new UnauthorizedAccessException("user-token-required");
        return Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub")!);
    }

    [HttpPost]
    public async Task<ActionResult<CreateSessionResponse>> Create([FromBody] CreateSessionRequest req)
    {
        var uid = RequireUserId();
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == req.DeviceId && !d.Revoked);
        if (device is null) return NotFound(new { error = "device-not-found" });
        if (device.OwnerId != uid) return Forbid();
        if (!SignalingHub.IsAgentOnline(device.Id))
            return StatusCode(503, new { error = "device-offline" });

        var session = new Session
        {
            DeviceId = device.Id,
            InitiatorUserId = uid,
            Status = SessionStatus.Requested,
            InitiatorIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
        };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        var sessionToken = _jwt.CreateSessionToken(session.Id, uid, "session-viewer");
        return Ok(new CreateSessionResponse(session.Id, sessionToken));
    }

    [HttpGet("{id:guid}/turn")]
    public ActionResult<TurnCredentialResponse> Turn(Guid id)
    {
        var kind = User.FindFirst("kind")?.Value;
        if (kind != "user" && kind != "agent") return Forbid();

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!;
        var creds = _turn.Issue($"{kind}:{subject}:{id}");
        return Ok(new TurnCredentialResponse(
            creds.Username, creds.Password, creds.TtlSeconds, creds.StunUris, creds.TurnUris));
    }
}
