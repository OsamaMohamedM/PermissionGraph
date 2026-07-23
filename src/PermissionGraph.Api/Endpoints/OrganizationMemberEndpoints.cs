namespace PermissionGraph.Api.Endpoints;

public static class OrganizationMemberEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var members = app.MapGroup("/api/v1/organizations/{organizationId:guid}")
            .RequireAuthorization();

        members.MapPost("/members", AddAsync)
            .RequirePermission("pg.members.manage")
            .RequireRateLimiting("org-member-add")
            .AddEndpointFilter<ValidationFilter<AddOrganizationMemberRequest>>();

        members.MapGet("/members", ListAsync)
            .AddEndpointFilter<ValidationFilter<ListOrganizationMembersRequest>>();

        members.MapGet("/members/{userId:guid}", GetAsync);

        members.MapPost("/members/{userId:guid}/suspend", SuspendAsync)
            .RequirePermission("pg.members.suspend")
            .RequireRateLimiting("org-member-mutations");

        members.MapPost("/members/{userId:guid}/reactivate", ReactivateAsync)
            .RequirePermission("pg.members.manage")
            .RequireRateLimiting("org-member-mutations");

        members.MapDelete("/members/{userId:guid}", RemoveAsync)
            .RequirePermission("pg.members.remove")
            .RequireRateLimiting("org-member-mutations");

        members.MapPost("/leave", LeaveAsync)
            .RequireRateLimiting("org-member-mutations");

        return app;
    }

    private static async Task<IResult> AddAsync(
        Guid organizationId,
        AddOrganizationMemberRequest request,
        AddOrganizationMemberHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(organizationId), cancellationToken);
        return Results.Created($"/api/v1/organizations/{organizationId}/members/{result.UserId}", result.ToResponse());
    }

    private static async Task<IResult> ListAsync(
        Guid organizationId,
        [AsParameters] ListOrganizationMembersRequest request,
        ListOrganizationMembersHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new ListOrganizationMembersQuery(organizationId, request.PageSize, request.Cursor, request.Search, request.Status);
        var result = await handler.HandleAsync(query, cancellationToken);
        return Results.Ok(new OrganizationMemberListResponse(result.Items.Select(item => item.ToResponse()).ToArray(), result.NextCursor, request.PageSize));
    }

    private static async Task<IResult> GetAsync(
        Guid organizationId,
        Guid userId,
        GetOrganizationMemberHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetOrganizationMemberQuery(organizationId, userId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }

    private static async Task<IResult> SuspendAsync(
        Guid organizationId,
        Guid userId,
        SuspendOrganizationMemberHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new SuspendOrganizationMemberCommand(organizationId, userId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ReactivateAsync(
        Guid organizationId,
        Guid userId,
        ReactivateOrganizationMemberHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new ReactivateOrganizationMemberCommand(organizationId, userId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveAsync(
        Guid organizationId,
        Guid userId,
        RemoveOrganizationMemberHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new RemoveOrganizationMemberCommand(organizationId, userId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> LeaveAsync(
        Guid organizationId,
        LeaveOrganizationHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new LeaveOrganizationCommand(organizationId), cancellationToken);
        return Results.NoContent();
    }
}

public sealed record ListOrganizationMembersRequest(
    [property: FromQuery] int PageSize = 20,
    [property: FromQuery] string? Cursor = null,
    [property: FromQuery] string? Search = null,
    [property: FromQuery] string? Status = null);
