namespace PermissionGraph.Api.Endpoints;

public static class RoleEndpoints
{
    private const string GetRoleEndpointName = "GetRole";

    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var roles = app.MapGroup("/api/v1/organizations/{organizationId:guid}/roles")
            .RequireAuthorization();

        roles.MapGet("/", ListAsync)
            .AddEndpointFilter<ValidationFilter<ListRolesRequest>>();

        roles.MapPost("/", CreateAsync)
            .RequireRateLimiting("org-mutations")
            .AddEndpointFilter<ValidationFilter<CreateCustomRoleRequest>>();

        roles.MapGet("/{roleId:guid}", GetAsync)
            .WithName(GetRoleEndpointName);

        roles.MapPatch("/{roleId:guid}", UpdateAsync)
            .RequireRateLimiting("org-mutations")
            .AddEndpointFilter<ValidationFilter<UpdateCustomRoleRequest>>();

        roles.MapPost("/{roleId:guid}/clone", CloneAsync)
            .RequireRateLimiting("org-mutations")
            .AddEndpointFilter<ValidationFilter<CloneRoleRequest>>();

        roles.MapPost("/{roleId:guid}/archive", ArchiveAsync)
            .RequireRateLimiting("org-mutations");

        roles.MapPost("/{roleId:guid}/activate", ActivateAsync)
            .RequireRateLimiting("org-mutations");

        roles.MapPut("/{roleId:guid}/permissions", ReplacePermissionsAsync)
            .RequireRateLimiting("org-mutations")
            .AddEndpointFilter<ValidationFilter<ReplaceRolePermissionsRequest>>();

        return app;
    }

    private static async Task<IResult> ListAsync(
        Guid organizationId,
        [AsParameters] ListRolesRequest request,
        ListRolesHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToQuery(organizationId), cancellationToken);
        return Results.Ok(new RoleListResponse(
            result.Items.Select(item => item.ToResponse()).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    private static async Task<IResult> CreateAsync(
        Guid organizationId,
        CreateCustomRoleRequest request,
        CreateCustomRoleHandler handler,
        LinkGenerator linkGenerator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(organizationId), cancellationToken);
        return CreatedRoleResult(organizationId, result, linkGenerator, httpContext);
    }

    private static async Task<IResult> GetAsync(
        Guid organizationId,
        Guid roleId,
        GetRoleHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetRoleQuery(organizationId, roleId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }

    private static async Task<IResult> UpdateAsync(
        Guid organizationId,
        Guid roleId,
        UpdateCustomRoleRequest request,
        UpdateCustomRoleHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(organizationId, roleId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }

    private static async Task<IResult> CloneAsync(
        Guid organizationId,
        Guid roleId,
        CloneRoleRequest request,
        CloneRoleHandler handler,
        LinkGenerator linkGenerator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(organizationId, roleId), cancellationToken);
        return CreatedRoleResult(organizationId, result, linkGenerator, httpContext);
    }

    private static async Task<IResult> ArchiveAsync(
        Guid organizationId,
        Guid roleId,
        ArchiveCustomRoleHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new ArchiveCustomRoleCommand(organizationId, roleId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ActivateAsync(
        Guid organizationId,
        Guid roleId,
        ActivateCustomRoleHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new ActivateCustomRoleCommand(organizationId, roleId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ReplacePermissionsAsync(
        Guid organizationId,
        Guid roleId,
        ReplaceRolePermissionsRequest request,
        ReplaceRolePermissionsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(organizationId, roleId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }

    private static IResult CreatedRoleResult(
        Guid organizationId,
        RoleResult result,
        LinkGenerator linkGenerator,
        HttpContext httpContext)
    {
        var location = linkGenerator.GetPathByName(
            httpContext,
            GetRoleEndpointName,
            new { organizationId, roleId = result.Id });

        if (location is null)
        {
            throw new InvalidOperationException("Role get endpoint link could not be generated.");
        }

        return Results.Created(location, result.ToResponse());
    }
}

public sealed record ListRolesRequest(
    [property: FromQuery] string? RoleType = null,
    [property: FromQuery] string? ScopeType = null,
    [property: FromQuery] bool? IsActive = null,
    [property: FromQuery] bool? IsRequestable = null,
    [property: FromQuery] string? Search = null,
    [property: FromQuery] int Page = 1,
    [property: FromQuery] int PageSize = 20);
