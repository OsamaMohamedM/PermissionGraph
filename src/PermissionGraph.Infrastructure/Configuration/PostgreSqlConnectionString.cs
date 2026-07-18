using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PermissionGraph.Infrastructure.Configuration;

public static class PostgreSqlConnectionString
{
    public static string FromConfiguration(IConfiguration configuration)
    {
        var configured = configuration["ConnectionStrings:PermissionGraph"];

        if (configured is not null)
        {
            return configured;
        }

        return FromEnvironment();
    }

    public static string FromEnvironment()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost",
            Port = ReadPort(),
            Database = RequireEnvironmentVariable("POSTGRES_DB"),
            Username = RequireEnvironmentVariable("POSTGRES_USER"),
            Password = RequireEnvironmentVariable("POSTGRES_PASSWORD")
        };

        return builder.ConnectionString;
    }

    private static int ReadPort()
    {
        var value = Environment.GetEnvironmentVariable("POSTGRES_PORT");

        if (string.IsNullOrWhiteSpace(value))
        {
            return 5432;
        }

        return int.TryParse(value, out var port)
            ? port
            : throw new InvalidOperationException("POSTGRES_PORT must be a valid integer.");
    }

    private static string RequireEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required environment variable '{name}'.");
        }

        return value;
    }
}
