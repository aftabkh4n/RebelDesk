using System.ComponentModel.DataAnnotations;

namespace RebelDesk.Server.Data;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(320)] public string Email { get; set; } = default!;
    [MaxLength(128)] public string DisplayName { get; set; } = default!;
    [MaxLength(200)] public string PasswordHash { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<Device> Devices { get; set; } = new();
}

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = default!;
    [MaxLength(128)] public string DeviceName { get; set; } = default!;
    [MaxLength(64)] public string OsPlatform { get; set; } = "Windows";
    [MaxLength(2048)] public string PublicKey { get; set; } = default!;
    [MaxLength(200)] public string AgentTokenHash { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenUtc { get; set; }
    public bool Revoked { get; set; }
}

public class PairingCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(16)] public string Code { get; set; } = default!;
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = default!;
    [MaxLength(128)] public string SuggestedDeviceName { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public Guid? ConsumedByDeviceId { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
}

public enum SessionStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
    Active = 3,
    Ended = 4,
    Expired = 5,
}

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = default!;
    public Guid InitiatorUserId { get; set; }
    public User InitiatorUser { get; set; } = default!;
    public SessionStatus Status { get; set; } = SessionStatus.Requested;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    [MaxLength(45)] public string? InitiatorIp { get; set; }
    [MaxLength(256)] public string? EndReason { get; set; }
}
