using Microsoft.AspNetCore.Mvc;
using PermissionGraph.Api.Endpoints.Mapping;
using PermissionGraph.Api.Validation;
using PermissionGraph.Application.Features.Permissions;
using PermissionGraph.Contracts.Permissions;

namespace PermissionGraph.Api.Endpoints;

public static class PermissionEndpoints
{
    private const string GetPermissionEndpointName = "GetPermission";

    public static IEndpointRouteBuilder MapPermissionEndpoints(this IEndpointRouteBuilder app)
    {
        var permissions = app.MapGroup("/api/v1/organizations/{organizationId:guid}/permissions")
            .RequireAuthorization();

        permissions.MapGet("/", ListAsync)
            .AddEndpointFilter<ValidationFilter<ListPermissionsRequest>>();

        permissions.MapPost("/", CreateAsync)
            .RequireRateLimiting("org-mutations")
            .AddEndpointFilter<ValidationFilter<CreateCustomPermissionRequest>>();

        permissions.MapGet("/{permissionId:guid}", GetAsync)
            .WithName(GetPermissionEndpointName);

        permissions.MapPatch("/{permissionId:guid}", UpdateAsync)
            .RequireRateLimiting("org-mutations")
            .AddEndpointFilter<ValidationFilter<UpdateCustomPermissionRequest>>();

        permissions.MapPost("/{permissionId:guid}/archive", ArchiveAsync)
            .RequireRateLimiting("org-mutations");

        permissions.MapPost("/{permissionId:guid}/activate", ActivateAsync)
            .RequireRateLimiting("org-mutations");

        return app;
    }

    private static async Task<IResult> ListAsync(
        Guid organizationId,
        [AsParameters] ListPermissionsRequest request,
        ListPermissionsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToQuery(organizationId), cancellationToken);
        return Results.Ok(new PermissionListResponse(
            result.Items.Select(item => item.ToResponse()).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    private static async Task<IResult> CreateAsync(
        Guid organizationId,
        CreateCustomPermissionRequest request,
        CreateCustomPermissionHandler handler,
        LinkGenerator linkGenerator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(organizationId), cancellationToken);
        var location = linkGenerator.GetPathByName(
            httpContext,
            GetPermissionEndpointName,
            new { organizationId, permissionId = result.Id });

        if (location is null)
        {
            throw new InvalidOperationException("Permission get endpoint link could not be generated.");
        }

        return Results.Created(location, result.ToResponse());
    }

    private static async Task<IResult> GetAsync(
        Guid organizationId,
        Guid permissionId,
        GetPermissionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetPermissionQuery(organizationId, permissionId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }

    private static async Task<IResult> UpdateAsync(
        Guid organizationId,
        Guid permissionId,
        UpdateCustomPermissionRequest request,
        UpdateCustomPermissionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(organizationId, permissionId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }

    private static async Task<IResult> ArchiveAsync(
        Guid organizationId,
        Guid permissionId,
        ArchiveCustomPermissionHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new ArchiveCustomPermissionCommand(organizationId, permissionId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ActivateAsync(
        Guid organizationId,
        Guid permissionId,
        ActivateCustomPermissionHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new ActivateCustomPermissionCommand(organizationId, permissionId), cancellationToken);
        return Results.NoContent();
    }
}

public sealed record ListPermissionsRequest(
    [property: FromQuery] string? PermissionType = null,
    [property: FromQuery] string? Module = null,
    [property: FromQuery] bool? IsActive = null,
    [property: FromQuery] bool? IsRequestable = null,
    [property: FromQuery] string? AllowedScope = null,
    [property: FromQuery] string? Search = null,
    [property: FromQuery] int Page = 1,
    [property: FromQuery] int PageSize = 20);
