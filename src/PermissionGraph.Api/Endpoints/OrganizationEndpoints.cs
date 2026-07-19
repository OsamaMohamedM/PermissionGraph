using Microsoft.AspNetCore.Mvc;
using PermissionGraph.Api.Validation;
using PermissionGraph.Application.Features.Organizations;
using PermissionGraph.Contracts.Organizations;

namespace PermissionGraph.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var organizations = app.MapGroup("/api/v1/organizations")
            .RequireAuthorization();

        organizations.MapPost("/", CreateAsync)
            .RequireRateLimiting("org-mutations")
            .AddEndpointFilter<ValidationFilter<CreateOrganizationRequest>>();

        organizations.MapGet("/", ListAsync)
            .AddEndpointFilter<ValidationFilter<ListOrganizationsRequest>>();

        organizations.MapGet("/{organizationId:guid}", GetAsync);

        organizations.MapPatch("/{organizationId:guid}", UpdateAsync)
            .RequireRateLimiting("org-mutations")
            .AddEndpointFilter<ValidationFilter<UpdateOrganizationRequest>>();

        organizations.MapPost("/{organizationId:guid}/archive", ArchiveAsync)
            .RequireRateLimiting("org-mutations")
            .AddEndpointFilter<ValidationFilter<ArchiveOrganizationRequest>>();

        organizations.MapPost("/{organizationId:guid}/transfer-ownership", TransferOwnershipAsync)
            .RequireRateLimiting("org-transfer-ownership")
            .AddEndpointFilter<ValidationFilter<TransferOwnershipRequest>>();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateOrganizationRequest request,
        CreateOrganizationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(), cancellationToken);
        return Results.Created($"/api/v1/organizations/{result.Id}", result.ToResponse());
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] ListOrganizationsRequest request,
        ListOrganizationsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListOrganizationsQuery(request.PageSize, request.Cursor), cancellationToken);
        return Results.Ok(new OrganizationListResponse(result.Items.Select(item => item.ToResponse()).ToArray(), result.NextCursor, request.PageSize));
    }

    private static async Task<IResult> GetAsync(
        Guid organizationId,
        GetOrganizationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetOrganizationQuery(organizationId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }

    private static async Task<IResult> UpdateAsync(
        Guid organizationId,
        UpdateOrganizationRequest request,
        UpdateOrganizationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(organizationId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }

    private static async Task<IResult> ArchiveAsync(
        Guid organizationId,
        ArchiveOrganizationRequest request,
        ArchiveOrganizationHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request.ToCommand(organizationId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> TransferOwnershipAsync(
        Guid organizationId,
        TransferOwnershipRequest request,
        TransferOwnershipHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(organizationId), cancellationToken);
        return Results.Ok(result.ToResponse());
    }
}

public sealed record ListOrganizationsRequest([property: FromQuery] int PageSize = 20, [property: FromQuery] string? Cursor = null);
