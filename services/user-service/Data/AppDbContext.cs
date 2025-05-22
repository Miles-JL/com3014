using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace UserService.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) {}

        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired();
            });
        }

        public async Task SeedAsync(bool isDevelopment = false)
        {
            if (isDevelopment)
            {
                // Ensure the database is created and migrations are applied
                await Database.EnsureDeletedAsync();
                await Database.EnsureCreatedAsync();

                // Add test users if the database is empty
                if (!Users.Any())
                {
                    var testUser = new User
                    {
                        Id = 1,
                        Username = "testuser",
                        ProfileDescription = "Test User",
                        CreatedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    Users.Add(testUser);
                    await SaveChangesAsync();
                }
            }
            else if (!Users.Any())
            {
                // In production, just ensure the database is created
                await Database.EnsureCreatedAsync();
            }
        }
    }
}
