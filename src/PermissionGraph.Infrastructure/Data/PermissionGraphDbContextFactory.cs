namespace PermissionGraph.Infrastructure.Data;

public sealed class PermissionGraphDbContextFactory : IDesignTimeDbContextFactory<PermissionGraphDbContext>
{
    public PermissionGraphDbContext CreateDbContext(string[] args)
    {
        LocalEnvironmentFileHelper.LoadIfPresent();

        var connectionString = Environment.GetEnvironmentVariable("PERMISSIONGRAPH_DATABASE")
            ?? PostgreSqlConnectionStringHelper.FromEnvironment();

        var options = new DbContextOptionsBuilder<PermissionGraphDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PermissionGraphDbContext(options);
    }
}