namespace RebelDesk.Server.Models;

public record RegisterRequest(string Email, string DisplayName, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, Guid UserId, string Email, string DisplayName);

public record CreatePairingCodeRequest(string SuggestedDeviceName);
public record CreatePairingCodeResponse(string Code, DateTime ExpiresAtUtc);

public record ConsumePairingRequest(string Code, string DeviceName, string OsPlatform, string PublicKey);
public record ConsumePairingResponse(Guid DeviceId, string AgentToken, string RefreshToken);

public record RefreshAgentRequest(Guid DeviceId, string RefreshToken);
public record DeviceSummary(Guid Id, string DeviceName, string OsPlatform, DateTime? LastSeenUtc, bool Online);

public record CreateSessionRequest(Guid DeviceId);
public record CreateSessionResponse(Guid SessionId, string SessionToken);

public record TurnCredentialResponse(string Username, string Password, long Ttl, string[] StunUris, string[] TurnUris);

public record SignalingMessage(string Type, string? Sdp, string? Candidate, string? SdpMid, int? SdpMLineIndex);
