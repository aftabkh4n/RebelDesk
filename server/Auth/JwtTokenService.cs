using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace RebelDesk.Server.Auth;

public sealed record JwtOptions
{
    public string Issuer { get; init; } = "rebeldesk";
    public string Audience { get; init; } = "rebeldesk";
    public string SigningKey { get; init; } = default!;
    public int UserTokenMinutes { get; init; } = 60 * 8;
    public int AgentTokenMinutes { get; init; } = 60 * 24 * 30;
    public int SessionTokenMinutes { get; init; } = 10;
}

public class JwtTokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _creds;
    private readonly SymmetricSecurityKey _key;

    public JwtTokenService(JwtOptions options)
    {
        _options = options;
        var keyBytes = Encoding.UTF8.GetBytes(options.SigningKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt SigningKey must be at least 32 bytes. Configure Auth:SigningKey.");
        _key = new SymmetricSecurityKey(keyBytes);
        _creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
    }

    public SymmetricSecurityKey Key => _key;
    public JwtOptions Options => _options;

    public string CreateUserToken(Guid userId, string email)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("kind", "user"),
        };
        return Emit(claims, TimeSpan.FromMinutes(_options.UserTokenMinutes));
    }

    public string CreateAgentToken(Guid deviceId, Guid ownerId)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, deviceId.ToString()),
            new Claim("owner", ownerId.ToString()),
            new Claim("kind", "agent"),
        };
        return Emit(claims, TimeSpan.FromMinutes(_options.AgentTokenMinutes));
    }

    public string CreateSessionToken(Guid sessionId, Guid subjectId, string kind)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, subjectId.ToString()),
            new Claim("session", sessionId.ToString()),
            new Claim("kind", kind),
        };
        return Emit(claims, TimeSpan.FromMinutes(_options.SessionTokenMinutes));
    }

    private string Emit(Claim[] claims, TimeSpan lifetime)
    {
        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: _creds);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public static string GenerateAgentRefresh() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

    public static string HashRefresh(string refresh)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refresh));
        return Convert.ToHexString(bytes);
    }
}
