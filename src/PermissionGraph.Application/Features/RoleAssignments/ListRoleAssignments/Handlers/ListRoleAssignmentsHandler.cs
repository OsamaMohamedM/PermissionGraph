namespace PermissionGraph.Application.Features.RoleAssignments.ListRoleAssignments.Handlers;

public sealed class ListRoleAssignmentsHandler(
    IValidator<ListRoleAssignmentsQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccessHelper organizationAccess,
    IRoleAssignmentRepository assignmentRepository)
{
    public async Task<PageResult<RoleAssignmentResult>> HandleAsync(
        ListRoleAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await organizationAccess.RequireVisibleActiveOrganizationAsync(
            query.OrganizationId,
            actor.UserId,
            cancellationToken);

        var filters = new RoleAssignmentListFilters(
            query.UserId,
            query.RoleId,
            query.ScopeType,
            query.ScopeId,
            query.Status,
            query.EffectiveAtUtc,
            query.ExpiringBeforeUtc);
        var result = await assignmentRepository.ListVisibleForOrganizationAsync(
            organization.Id,
            filters,
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PageResult<RoleAssignmentResult>(
            result.Items.Select(RoleAssignmentResult.FromDomain).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }
}
