Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console()
    .CreateLogger();

try
{
    LocalEnvironmentFileHelper.LoadIfPresent();

    var builder = WebApplication.CreateBuilder(args);

    StartupValidation.ValidateFoundationConfiguration(builder.Configuration);

    builder.Host.UseSerilog();
    builder.Services.AddOpenApi();
    builder.Services.AddApiValidation();
    builder.Services.AddPermissionGraphApplication();
    builder.Services.AddPermissionGraphInfrastructure(builder.Configuration);
    builder.Services.AddApiExceptionHandling();
    builder.Services.AddApiAuthentication(builder.Configuration);
    builder.Services.AddApiAuthorization();
    builder.Services.AddApiRateLimiting();
    builder.Services.AddApiHealthChecks(builder.Configuration);

    var app = builder.Build();

    app.UseApiPipeline();
    app.MapApiEndpoints();

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "PermissionGraph API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;