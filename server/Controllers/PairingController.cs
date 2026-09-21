using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RebelDesk.Server.Auth;
using RebelDesk.Server.Data;
using RebelDesk.Server.Models;

namespace RebelDesk.Server.Controllers;

[ApiController]
[Route("api/pairing")]
public class PairingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    public PairingController(AppDbContext db, JwtTokenService jwt) { _db = db; _jwt = jwt; }

    [HttpPost("consume")]
    public async Task<ActionResult<ConsumePairingResponse>> Consume([FromBody] ConsumePairingRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Code)) return BadRequest(new { error = "code-required" });
        if (string.IsNullOrWhiteSpace(req.DeviceName)) return BadRequest(new { error = "device-name-required" });
        if (string.IsNullOrWhiteSpace(req.PublicKey)) return BadRequest(new { error = "public-key-required" });

        var code = req.Code.Trim().ToUpperInvariant();
        var pc = await _db.PairingCodes.FirstOrDefaultAsync(p => p.Code == code);
        if (pc is null) return NotFound(new { error = "code-invalid" });
        if (pc.ConsumedByDeviceId != null) return Conflict(new { error = "code-already-used" });
        if (pc.ExpiresAtUtc < DateTime.UtcNow) return BadRequest(new { error = "code-expired" });

        var refresh = JwtTokenService.GenerateAgentRefresh();
        var device = new Device
        {
            OwnerId = pc.OwnerId,
            DeviceName = req.DeviceName.Trim(),
            OsPlatform = string.IsNullOrWhiteSpace(req.OsPlatform) ? "Windows" : req.OsPlatform.Trim(),
            PublicKey = req.PublicKey.Trim(),
            AgentTokenHash = JwtTokenService.HashRefresh(refresh),
        };
        _db.Devices.Add(device);
        pc.ConsumedByDeviceId = device.Id;
        pc.ConsumedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var agentToken = _jwt.CreateAgentToken(device.Id, device.OwnerId);
        return Ok(new ConsumePairingResponse(device.Id, agentToken, refresh));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ConsumePairingResponse>> Refresh([FromBody] RefreshAgentRequest req)
    {
        var device = await _db.Devices.FindAsync(req.DeviceId);
        if (device is null || device.Revoked) return Unauthorized(new { error = "device-revoked-or-missing" });
        if (device.AgentTokenHash != JwtTokenService.HashRefresh(req.RefreshToken ?? ""))
            return Unauthorized(new { error = "refresh-invalid" });

        var newRefresh = JwtTokenService.GenerateAgentRefresh();
        device.AgentTokenHash = JwtTokenService.HashRefresh(newRefresh);
        device.LastSeenUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = _jwt.CreateAgentToken(device.Id, device.OwnerId);
        return Ok(new ConsumePairingResponse(device.Id, token, newRefresh));
    }
}
