namespace PermissionGraph.Api.Endpoints;

public static class AuthorizationEndpoints
{
    public static IEndpointRouteBuilder MapAuthorizationEndpoints(this IEndpointRouteBuilder app)
    {
        var authorization = app.MapGroup("/api/v1/organizations/{organizationId:guid}/authorization")
            .RequireAuthorization();

        authorization.MapPost("/check", CheckAsync)
            .RequirePermission("pg.authorization.check")
            .AddEndpointFilter<ValidationFilter<AuthorizationCheckRequest>>();

        authorization.MapPost("/batch-check", BatchCheckAsync)
            .RequirePermission("pg.authorization.check")
            .AddEndpointFilter<ValidationFilter<AuthorizationBatchCheckRequest>>();

        authorization.MapPost("/explain", ExplainAsync)
            .RequireRateLimiting("authorization-explain")
            .AddEndpointFilter<ValidationFilter<ExplainAccessRequest>>();

        return app;
    }

    private static async Task<IResult> CheckAsync(
        Guid organizationId,
        AuthorizationCheckRequest request,
        IAuthorizationDecisionService authorizationDecisionService,
        CancellationToken cancellationToken)
    {
        var decision = await authorizationDecisionService.CheckAsync(request.ToQuery(organizationId), cancellationToken);
        return Results.Ok(decision.ToResponse());
    }

    private static async Task<IResult> BatchCheckAsync(
        Guid organizationId,
        AuthorizationBatchCheckRequest request,
        IAuthorizationDecisionService authorizationDecisionService,
        CancellationToken cancellationToken)
    {
        var result = await authorizationDecisionService.BatchCheckAsync(request.ToQuery(organizationId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }

    private static async Task<IResult> ExplainAsync(
        Guid organizationId,
        ExplainAccessRequest request,
        ExplainAccessHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToQuery(organizationId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }
}
