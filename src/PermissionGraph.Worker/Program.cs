using PermissionGraph.Infrastructure.Configuration;
using PermissionGraph.Infrastructure.DependencyInjection;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console()
    .CreateLogger();

try
{
    LocalEnvironmentFile.LoadIfPresent();

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddPermissionGraphInfrastructure(builder.Configuration);

    var host = builder.Build();
    host.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "PermissionGraph Worker terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
