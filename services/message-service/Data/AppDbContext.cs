using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace MessageService.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<DirectMessage> DirectMessages { get; set; }

        public async Task SeedAsync(bool isDevelopment = false)
        {
            if (DirectMessages.Any())
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
