using Microsoft.EntityFrameworkCore;
using Shared.Models;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using System.Net.Http.Json;

namespace AuthService.Data
{
    public class AppDbContext : DbContext
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AppDbContext> _logger;

        public AppDbContext(DbContextOptions<AppDbContext> options, IHttpClientFactory httpClientFactory, ILogger<AppDbContext> logger)
            : base(options) 
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public DbSet<User> Users => Set<User>();

        public async Task SeedFromCsvAsync(string csvPath, bool isDevelopment = false)
        {
            try
            {
                if (Users.Any())
                {
                    if (isDevelopment)
                    {
                        Database.EnsureDeleted();
                        Database.EnsureCreated();
                    }
                    else
                    {
                        return;
                    }
                }

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    HeaderValidated = null,
                    MissingFieldFound = null,
                    BadDataFound = null
                };

                using var reader = new StreamReader(csvPath);
                using var csv = new CsvReader(reader, config);
                
                var records = csv.GetRecordsAsync<CsvUser>();
                var httpClient = _httpClientFactory.CreateClient();
                
                await foreach (var record in records)
                {
                    var user = new User
                    {
                        Username = record.Username,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(record.Password),
                        ProfileImage = record.ProfileImage,
                        ProfileDescription = record.ProfileDescription,
                        Location = record.Location,
                        IsAdmin = record.IsAdmin,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    Users.Add(user);
                    await SaveChangesAsync();

                    _logger.LogInformation("Successfully added user: {Username}", user.Username);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired();
            });
        }
    }

    // Class to match CSV structure
    public class CsvUser
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ProfileImage { get; set; } = string.Empty;
        public string ProfileDescription { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }
    }
