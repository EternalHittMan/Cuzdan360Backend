using Cuzdan360Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Cuzdan360Backend.Configurations
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            // Use a dummy connection string for migration generation if needed, 
            // or try to read from appsettings.Development.json but handle failure.
            // For 'dotnet ef migrations add', we just need the model definition, 
            // but EF often validates connection syntax.
            
            // NOTE: Must ensure the server version syntax is correct even for dummy.
            var connectionString = "Server=localhost;Database=myaimoderator;User=root;Password=test123;";

            optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 44)));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
