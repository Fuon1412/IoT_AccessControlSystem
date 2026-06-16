using Microsoft.EntityFrameworkCore;
using IoTAccessAPI.Models;

namespace IoTAccessAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<AccessLog> AccessLogs { get; set; }
    public DbSet<RfidCard> RfidCards { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Role).HasDefaultValue("Employee");
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasIndex(d => d.MacAddress).IsUnique();
            entity.HasIndex(d => d.Name).IsUnique(); // prevent duplicate device names
        });

        modelBuilder.Entity<AccessLog>(entity =>
        {
            entity.HasIndex(a => a.RequestId).IsUnique(); // idempotency
            entity.HasOne(a => a.Device)
                  .WithMany(d => d.AccessLogs)
                  .HasForeignKey(a => a.DeviceId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.User)
                  .WithMany()
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RfidCard>(entity =>
        {
            entity.HasIndex(r => r.Uid).IsUnique();
            entity.HasOne(r => r.User)
                  .WithMany()
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.SetNull); // unassign card if user deleted
        });
    }
}
