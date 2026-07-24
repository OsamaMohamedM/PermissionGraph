namespace PermissionGraph.Application.Features.RoleAssignments.GetRoleAssignment.Handlers;

public sealed class GetRoleAssignmentHandler(
    IValidator<GetRoleAssignmentQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccessHelper organizationAccess,
    IRoleAssignmentRepository assignmentRepository)
{
    public async Task<RoleAssignmentResult> HandleAsync(
        GetRoleAssignmentQuery query,
        CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        await organizationAccess.RequireVisibleActiveOrganizationAsync(
            query.OrganizationId,
            actor.UserId,
            cancellationToken);

        var assignment = await assignmentRepository.GetVisibleByOrganizationAndIdAsync(
            query.OrganizationId,
            query.AssignmentId,
            cancellationToken);

        return assignment is null
            ? throw RevokeRoleAssignmentHandler.NotFound()
            : RoleAssignmentResult.FromDomain(assignment);
    }
}
