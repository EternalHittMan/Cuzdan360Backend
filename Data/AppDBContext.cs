using Cuzdan360Backend.Models;
using Microsoft.EntityFrameworkCore;
using Cuzdan360Backend.Models;

namespace Cuzdan360Backend.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Kullanıcı tablosu için varsayılan değerler ekleyebilirsin
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1, Username = "admin",
                PasswordHash = "$2a$12$vDN5rfJgTGOrCvJ0354EueBJhTkQOt3cWqCnInML7TKC9qbDv/cYK",
                Email = "admin@example.com"
            }
        );
    }
}