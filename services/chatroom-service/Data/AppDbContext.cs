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
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                entity.HasOne(e => e.Creator)
                      .WithMany()
                      .HasForeignKey(e => e.CreatorId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.LastUpdated).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                // Set default values for other required properties
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.ProfileImage).IsRequired().HasDefaultValue("default.jpg");
                entity.Property(e => e.ProfileDescription).HasDefaultValue(string.Empty);
                entity.Property(e => e.Location).HasDefaultValue(string.Empty);
                entity.Property(e => e.IsAdmin).HasDefaultValue(false);
            });
        }
        
        public async Task SeedAsync(bool isDevelopment = false)
        {
            try
            {
                // In development, we'll recreate the database each time
                if (isDevelopment)
                {
                    await Database.EnsureDeletedAsync();
                }
                
                // This will create the database and apply any pending migrations
                await Database.EnsureCreatedAsync();

                // Don't reseed if we already have data (in non-dev environments)
                if (!isDevelopment && (await ChatRooms.AnyAsync() || await Users.AnyAsync()))
                {
                    return;
                }

                // Add some test data if needed in development
                if (isDevelopment && !await Users.AnyAsync())
                {
                    // Add a test user
                    var testUser = new User 
                    { 
                        Id = 1,  // Using int to match the User model
                        Username = "testuser",
                        ProfileImage = "default.jpg",
                        CreatedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };
                    Users.Add(testUser);
                    await SaveChangesAsync();

                    // Add a test chatroom
                    var testRoom = new ChatRoom
                    {
                        Id = 1,  // Using int to match the ChatRoom model
                        Name = "General Chat",
                        Description = "General discussion",
                        CreatorId = testUser.Id,  // This should match the test user's ID
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    ChatRooms.Add(testRoom);
                    await SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // Log the error (in a real app, you'd want to use ILogger)
                Console.WriteLine($"Error seeding database: {ex.Message}");
                throw;
            }
        }
    }
}
