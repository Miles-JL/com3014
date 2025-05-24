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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure the Notification entity
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(n => n.RecipientId);
            entity.HasIndex(n => n.IsRead);
            entity.HasIndex(n => n.Timestamp);
        });
    }
}
