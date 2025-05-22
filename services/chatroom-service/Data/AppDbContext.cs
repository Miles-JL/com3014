using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace ChatroomService.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
        public DbSet<User> Users => Set<User>(); // For Creator relation

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ChatRoom>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();

                entity.HasOne(e => e.Creator)
                      .WithMany()
                      .HasForeignKey(e => e.CreatorId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired();
            });
        }
        
        public async Task SeedAsync(bool isDevelopment = false)
        {
            // Only clear if there are existing entries
            if (ChatRooms.Any() || Users.Any())
            {
                if (isDevelopment)
                {
                    // Drop and recreate the database schema
                    await Database.EnsureDeletedAsync();
                    await Database.EnsureCreatedAsync();
                }
            }
        }
    }
}
