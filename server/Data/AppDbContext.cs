using Microsoft.EntityFrameworkCore;

namespace RebelDesk.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<PairingCode> PairingCodes => Set<PairingCode>();
    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Device>().HasIndex(d => d.OwnerId);
        b.Entity<PairingCode>().HasIndex(p => p.Code).IsUnique();
        b.Entity<Session>().HasIndex(s => s.DeviceId);
        b.Entity<Session>().HasIndex(s => s.InitiatorUserId);
    }
}
