using Microsoft.EntityFrameworkCore;
using JwtAuthApi.Models;

namespace JwtAuthApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }  // ← This is required
}