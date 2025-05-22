using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace MessageService.Data;
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DirectMessage> DirectMessages { get; set; }
}
