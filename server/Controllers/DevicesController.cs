using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RebelDesk.Server.Data;
using RebelDesk.Server.Hubs;
using RebelDesk.Server.Models;

namespace RebelDesk.Server.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _db;
    public DevicesController(AppDbContext db) => _db = db;

    private Guid RequireUserId()
    {
        if (User.FindFirst("kind")?.Value != "user")
            throw new UnauthorizedAccessException("user-token-required");
        return Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub")!);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeviceSummary>>> List()
    {
        var uid = RequireUserId();
        var devices = await _db.Devices
            .Where(d => d.OwnerId == uid && !d.Revoked)
            .OrderByDescending(d => d.LastSeenUtc ?? d.CreatedAtUtc)
            .ToListAsync();
        return Ok(devices.Select(d => new DeviceSummary(
            d.Id, d.DeviceName, d.OsPlatform, d.LastSeenUtc, SignalingHub.IsAgentOnline(d.Id))));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var uid = RequireUserId();
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == uid);
        if (device is null) return NotFound();
        device.Revoked = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("pairing")]
    public async Task<ActionResult<CreatePairingCodeResponse>> CreatePairing(
        [FromBody] CreatePairingCodeRequest req)
    {
        var uid = RequireUserId();
        var code = GeneratePairingCode();
        var pc = new PairingCode
        {
            Code = code,
            OwnerId = uid,
            SuggestedDeviceName = string.IsNullOrWhiteSpace(req.SuggestedDeviceName) ? "Windows PC" : req.SuggestedDeviceName.Trim(),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
        };
        _db.PairingCodes.Add(pc);
        await _db.SaveChangesAsync();
        return Ok(new CreatePairingCodeResponse(pc.Code, pc.ExpiresAtUtc));
    }

    private static string GeneratePairingCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var buf = RandomNumberGenerator.GetBytes(8);
        Span<char> chars = stackalloc char[9];
        for (int i = 0, j = 0; i < 8; i++)
        {
            chars[j++] = alphabet[buf[i] % alphabet.Length];
            if (j == 4) chars[j++] = '-';
        }
        return new string(chars);
    }
}
