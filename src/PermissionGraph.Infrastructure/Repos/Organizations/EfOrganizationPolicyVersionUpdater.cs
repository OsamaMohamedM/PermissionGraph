using Microsoft.EntityFrameworkCore;
using PermissionGraph.Application.Abstractions.Organizations;
using PermissionGraph.Domain.Organizations;
using PermissionGraph.Infrastructure.Data;

namespace PermissionGraph.Infrastructure.Organizations;

internal sealed class EfOrganizationPolicyVersionUpdater(PermissionGraphDbContext dbContext) : IOrganizationPolicyVersionUpdater
{
    public async Task IncrementPolicyVersionAsync(
        Guid organizationId,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations.SingleAsync(
            item => item.Id == organizationId,
            cancellationToken);

        var entry = dbContext.Entry(organization);
        entry.Property(nameof(Organization.PolicyVersion)).CurrentValue = organization.PolicyVersion + 1;
        entry.Property(nameof(Organization.UpdatedAtUtc)).CurrentValue = updatedAtUtc;
    }
}
