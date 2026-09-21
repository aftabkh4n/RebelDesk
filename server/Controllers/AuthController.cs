using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RebelDesk.Server.Auth;
using RebelDesk.Server.Data;
using RebelDesk.Server.Models;

namespace RebelDesk.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordService _passwords;
    private readonly JwtTokenService _jwt;

    public AuthController(AppDbContext db, PasswordService passwords, JwtTokenService jwt)
    {
        _db = db;
        _passwords = passwords;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            return BadRequest(new { error = "invalid-email" });
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
            return BadRequest(new { error = "password-too-short" });
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest(new { error = "display-name-required" });

        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { error = "email-in-use" });

        var user = new User
        {
            Email = email,
            DisplayName = req.DisplayName.Trim(),
            PasswordHash = _passwords.Hash(req.Password),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _jwt.CreateUserToken(user.Id, user.Email);
        return Ok(new AuthResponse(token, user.Id, user.Email, user.DisplayName));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !_passwords.Verify(req.Password ?? "", user.PasswordHash))
            return Unauthorized(new { error = "invalid-credentials" });

        var token = _jwt.CreateUserToken(user.Id, user.Email);
        return Ok(new AuthResponse(token, user.Id, user.Email, user.DisplayName));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> Me()
    {
        if (User.FindFirst("kind")?.Value != "user") return Forbid();
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? User.FindFirstValue("sub")!);
        var u = await _db.Users.FindAsync(id);
        return u is null ? NotFound() : Ok(new AuthResponse("", u.Id, u.Email, u.DisplayName));
    }
}
