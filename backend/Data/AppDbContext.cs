using JwtAuthApi.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Globalization;
using CsvHelper;

namespace JwtAuthApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Seeds the database with initial data from a CSV file.
    /// </summary>
    /// <param name="csvPath">Path to the CSV file containing user data.</param>
    /// <returns>void</returns>
    /// <exception cref="FileNotFoundException">Thrown when the CSV file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Users table is not empty.</exception>
    /// <exception cref="DbUpdateException">Thrown when there is an error saving changes to the database.</exception>
    /// <remarks>
    /// This method checks if the Users table is empty and if the CSV file exists.
    /// If both conditions are met, it reads the CSV file and adds the users to the database.
    /// </remarks>
    public void SeedUsersFromCsv(string csvPath)
    {
        // Check if the CSV file exists and the Users table is empty
        if (!Users.Any() && File.Exists(csvPath))
        {
            // Creates reader and CSV reader and reads the records from the CSV file
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<dynamic>();

            // For each record in the CSV file, create a new User object and add it to the Users DbSet
            foreach (var record in records)
            {
                Users.Add(new User
                {
                    Username = record.Username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(record.Password),
                    ProfileImage = record.ProfileImage,
                    ProfileDescription = record.ProfileDescription,
                    Location = record.Location,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                });
            }
            SaveChanges();
        }
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<ChatRoom> ChatRooms { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired();
        });
        
        // Configure ChatRoom entity
        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasOne(e => e.Creator)
                  .WithMany()
                  .HasForeignKey(e => e.CreatorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}