namespace PermissionGraph.Api.Configuration;

public static class ApiPipelineExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.DocumentTitle = "PermissionGraph API";
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "PermissionGraph API v1");
            options.RoutePrefix = "swagger";
            options.DisplayRequestDuration();
            options.EnablePersistAuthorization();
            options.EnableTryItOutByDefault();
        });
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();

        if (app.Environment.IsEnvironment("Testing"))
        {
            app.MapGet("/__test/problem", (HttpContext _) =>
                throw new InvalidOperationException("Test exception details"))
                .AllowAnonymous();

            app.MapPost("/__test/missing-validator", (MissingValidatorRequest _) => Results.NoContent())
                .AllowAnonymous()
                .AddEndpointFilter<ValidationFilter<MissingValidatorRequest>>();
        }

        app.MapHealthChecks("/health/live", HealthCheckResponseWriter.Live).AllowAnonymous();
        app.MapHealthChecks("/health/ready", HealthCheckResponseWriter.Ready).AllowAnonymous();
        app.MapAuthenticationEndpoints();
        app.MapOrganizationEndpoints();
        app.MapOrganizationMemberEndpoints();
        app.MapProjectEndpoints();
        app.MapPermissionEndpoints();
        app.MapRoleEndpoints();
        app.MapRoleAssignmentEndpoints();
        app.MapAuthorizationEndpoints();

        return app;
    }

    private sealed record MissingValidatorRequest(string Value);
}
