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
            if (Users.Any())
            {
                if (isDevelopment)
                {
                    await Database.EnsureDeletedAsync();
                    await Database.EnsureCreatedAsync();
                }
            }
        }
    }
}
