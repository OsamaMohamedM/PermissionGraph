using FluentValidation;
using PermissionGraph.Application.Abstractions.Memberships;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Validation;
using PermissionGraph.Application.Features.Organizations;
using PermissionGraph.Domain.Memberships;

namespace PermissionGraph.Application.Features.Memberships;

public sealed class GetOrganizationMemberHandler(
    IValidator<GetOrganizationMemberQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccess organizationAccess,
    IOrganizationMembershipRepository membershipRepository)
{
    public async Task<OrganizationMemberResult> HandleAsync(GetOrganizationMemberQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        await organizationAccess.RequireVisibleActiveOrganizationAsync(query.OrganizationId, actor.UserId, cancellationToken);

        var result = await membershipRepository.GetMemberResultAsync(query.OrganizationId, query.UserId, cancellationToken);
        if (result is null || result.Status == MembershipStatus.Removed)
        {
            throw OrganizationAccess.NotFound();
        }

        return result;
    }
}
