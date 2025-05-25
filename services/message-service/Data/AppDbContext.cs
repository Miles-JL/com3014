using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace MessageService.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<DirectMessage> DirectMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Configure your entity relationships and constraints here
            modelBuilder.Entity<DirectMessage>()
                .HasIndex(m => m.SenderId);
                
            modelBuilder.Entity<DirectMessage>()
                .HasIndex(m => m.RecipientId);
        }

        public async Task SeedAsync(bool isDevelopment = false)
        {
            if (isDevelopment && !DirectMessages.Any())
            {
                // Add test data here if needed
                await SaveChangesAsync();
            }
        }
    }
}
