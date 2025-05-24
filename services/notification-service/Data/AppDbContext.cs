using Microsoft.EntityFrameworkCore;
using notification_service.Models;

namespace notification_service.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) 
        : base(options) 
    {
    }

    public DbSet<Notification> Notifications { get; set; }
    public DbSet<PushSubscription> PushSubscriptions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure the Notification entity
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(n => n.UserId);
            entity.HasIndex(n => n.IsRead);
            entity.HasIndex(n => n.Timestamp);
        });

        // Configure the PushSubscription entity
        modelBuilder.Entity<PushSubscription>(entity =>
        {
            entity.HasIndex(p => p.UserId);
            entity.HasIndex(p => p.Endpoint).IsUnique();
            // Let's handle the default value in the migration directly
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql();
    }
}
