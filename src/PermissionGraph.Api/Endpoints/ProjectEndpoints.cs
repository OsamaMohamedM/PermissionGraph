namespace PermissionGraph.Api.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var projects = app.MapGroup("/api/v1/organizations/{organizationId:guid}/projects")
            .RequireAuthorization();

        projects.MapPost("/", CreateAsync)
            .RequirePermission("pg.projects.create")
            .RequireRateLimiting("org-mutations")
            .AddEndpointFilter<ValidationFilter<CreateProjectRequest>>();

        projects.MapGet("/", ListAsync)
            .AddEndpointFilter<ValidationFilter<ListProjectsRequest>>();

        projects.MapGet("/{projectId:guid}", GetAsync);

        projects.MapPatch("/{projectId:guid}", UpdateAsync)
            .RequirePermission("pg.projects.update")
            .RequireRateLimiting("org-mutations")
            .AddEndpointFilter<ValidationFilter<UpdateProjectRequest>>();

        projects.MapPost("/{projectId:guid}/archive", ArchiveAsync)
            .RequirePermission("pg.projects.archive")
            .RequireRateLimiting("org-mutations");

        return app;
    }

    private static async Task<IResult> CreateAsync(
        Guid organizationId,
        CreateProjectRequest request,
        CreateProjectHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(organizationId), cancellationToken);
        return Results.Created($"/api/v1/organizations/{organizationId}/projects/{result.Id}", result.ToResponse());
    }

    private static async Task<IResult> ListAsync(
        Guid organizationId,
        [AsParameters] ListProjectsRequest request,
        ListProjectsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToQuery(organizationId), cancellationToken);
        return Results.Ok(new ProjectListResponse(
            result.Items.Select(item => item.ToResponse()).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    private static async Task<IResult> GetAsync(
        Guid organizationId,
        Guid projectId,
        GetProjectHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetProjectQuery(organizationId, projectId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }

    private static async Task<IResult> UpdateAsync(
        Guid organizationId,
        Guid projectId,
        UpdateProjectRequest request,
        UpdateProjectHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(organizationId, projectId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }

    private static async Task<IResult> ArchiveAsync(
        Guid organizationId,
        Guid projectId,
        ArchiveProjectHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new ArchiveProjectCommand(organizationId, projectId, "ARCHIVE"), cancellationToken);
        return Results.NoContent();
    }
}

public sealed record ListProjectsRequest(
    [property: FromQuery] int Page = 1,
    [property: FromQuery] int PageSize = 20);
