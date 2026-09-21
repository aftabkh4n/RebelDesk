using System.Security.Cryptography;
using System.Text;

namespace RebelDesk.Server.Auth;

public sealed record TurnOptions
{
    public string StaticAuthSecret { get; init; } = default!;
    public string Realm { get; init; } = "rebeldesk";
    public string[] StunUris { get; init; } = Array.Empty<string>();
    public string[] TurnUris { get; init; } = Array.Empty<string>();
    public int TtlSeconds { get; init; } = 600;
}

public sealed record TurnCredentials(
    string Username,
    string Password,
    long TtlSeconds,
    string[] StunUris,
    string[] TurnUris);

/// <summary>
/// Issues short-lived TURN credentials compatible with coturn's <c>use-auth-secret</c> mode
/// (draft-uberti-behave-turn-rest-00 / RFC 7635 style ephemeral credentials).
/// Username = "&lt;unix-expiry&gt;:&lt;subjectId&gt;". Password = base64(HMAC-SHA1(secret, username)).
/// </summary>
public class TurnCredentialService
{
    private readonly TurnOptions _options;
    public TurnCredentialService(TurnOptions options) => _options = options;

    public TurnCredentials Issue(string subjectId)
    {
        if (string.IsNullOrWhiteSpace(_options.StaticAuthSecret))
            throw new InvalidOperationException("Turn:StaticAuthSecret is not configured.");

        var expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _options.TtlSeconds;
        var username = $"{expiry}:{subjectId}";
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_options.StaticAuthSecret));
        var mac = hmac.ComputeHash(Encoding.UTF8.GetBytes(username));
        var password = Convert.ToBase64String(mac);
        return new TurnCredentials(username, password, _options.TtlSeconds, _options.StunUris, _options.TurnUris);
    }
}
