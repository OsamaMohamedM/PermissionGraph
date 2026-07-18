using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PermissionGraph.Infrastructure.Configuration;

namespace PermissionGraph.Infrastructure.Data;

public sealed class PermissionGraphDbContextFactory : IDesignTimeDbContextFactory<PermissionGraphDbContext>
{
    public PermissionGraphDbContext CreateDbContext(string[] args)
    {
        LocalEnvironmentFile.LoadIfPresent();

        var connectionString = Environment.GetEnvironmentVariable("PERMISSIONGRAPH_DATABASE")
            ?? PostgreSqlConnectionString.FromEnvironment();

        var options = new DbContextOptionsBuilder<PermissionGraphDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PermissionGraphDbContext(options);
    }
}
