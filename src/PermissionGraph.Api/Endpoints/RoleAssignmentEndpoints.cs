namespace PermissionGraph.Api.Endpoints;

public static class RoleAssignmentEndpoints
{
    private const string GetRoleAssignmentEndpointName = "GetRoleAssignment";

    public static IEndpointRouteBuilder MapRoleAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        var assignments = app.MapGroup("/api/v1/organizations/{organizationId:guid}/role-assignments")
            .RequireAuthorization();

        assignments.MapPost("/", AssignAsync)
            .RequirePermission("pg.roles.assign")
            .RequireRateLimiting("org-mutations")
            .AddEndpointFilter<ValidationFilter<AssignRoleRequest>>();

        assignments.MapGet("/", ListAsync)
            .AddEndpointFilter<ValidationFilter<ListRoleAssignmentsRequest>>();

        assignments.MapGet("/{assignmentId:guid}", GetAsync)
            .WithName(GetRoleAssignmentEndpointName);

        assignments.MapPost("/{assignmentId:guid}/revoke", RevokeAsync)
            .RequirePermission("pg.roles.assign")
            .RequireRateLimiting("org-mutations")
            .AddEndpointFilter<ValidationFilter<RevokeRoleAssignmentRequest>>();

        return app;
    }

    private static async Task<IResult> AssignAsync(
        Guid organizationId,
        AssignRoleRequest request,
        AssignRoleHandler handler,
        LinkGenerator linkGenerator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(organizationId), cancellationToken);
        var location = linkGenerator.GetPathByName(
            httpContext,
            GetRoleAssignmentEndpointName,
            new { organizationId, assignmentId = result.Id });

        if (location is null)
        {
            throw new InvalidOperationException("Role assignment get endpoint link could not be generated.");
        }

        return Results.Created(location, result.ToResponse());
    }

    private static async Task<IResult> ListAsync(
        Guid organizationId,
        [AsParameters] ListRoleAssignmentsRequest request,
        ListRoleAssignmentsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToQuery(organizationId), cancellationToken);
        return Results.Ok(new RoleAssignmentListResponse(
            result.Items.Select(item => item.ToResponse()).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    private static async Task<IResult> GetAsync(
        Guid organizationId,
        Guid assignmentId,
        GetRoleAssignmentHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetRoleAssignmentQuery(organizationId, assignmentId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }

    private static async Task<IResult> RevokeAsync(
        Guid organizationId,
        Guid assignmentId,
        RevokeRoleAssignmentRequest request,
        RevokeRoleAssignmentHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(organizationId, assignmentId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }
}

public sealed record ListRoleAssignmentsRequest(
    [property: FromQuery] Guid? UserId = null,
    [property: FromQuery] Guid? RoleId = null,
    [property: FromQuery] string? ScopeType = null,
    [property: FromQuery] Guid? ScopeId = null,
    [property: FromQuery] string? Status = null,
    [property: FromQuery] DateTimeOffset? EffectiveAtUtc = null,
    [property: FromQuery] DateTimeOffset? ExpiringBeforeUtc = null,
    [property: FromQuery] int Page = 1,
    [property: FromQuery] int PageSize = 20);
