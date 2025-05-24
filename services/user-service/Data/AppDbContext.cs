using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using System.Globalization;

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

        public async Task SeedFromCsvAsync(string csvFilePath)
        {
            if (!File.Exists(csvFilePath))
            {
                throw new FileNotFoundException($"CSV file not found at path: {csvFilePath}");
            }

            using var reader = new StreamReader(csvFilePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            var users = new List<User>();
            
            // Read all records from CSV
            await foreach (var record in csv.GetRecordsAsync<dynamic>())
            {
                var user = new User
                {
                    Username = record.Username,
                    ProfileImage = record.ProfileImage,
                    ProfileDescription = record.ProfileDescription,
                    Location = record.Location,
                    IsAdmin = bool.Parse(record.IsAdmin),
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };

                users.Add(user);
            }

            // Add users to database if they don't already exist
            foreach (var user in users)
            {
                var existingUser = await Users.FirstOrDefaultAsync(u => u.Username == user.Username);
                if (existingUser == null)
                {
                    Users.Add(user);
                }
            }

            await SaveChangesAsync();
        }

        public async Task SeedAsync(bool isDevelopment = false)
        {
            // Ensure the database is created and migrations are applied
            await Database.EnsureCreatedAsync();

            if (isDevelopment)
            {
                await Database.EnsureDeletedAsync();
                await Database.EnsureCreatedAsync();

                // Try to seed from CSV first
                try
                {
                    var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "user_users_seed.csv");
                    await SeedFromCsvAsync(csvPath);
                    return; // Exit if CSV seeding was successful
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not seed from CSV: {ex.Message}");
                    // Fall back to test user if CSV seeding fails
                }

                // Fallback: Add test user if the database is empty
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
        }
    }
}
