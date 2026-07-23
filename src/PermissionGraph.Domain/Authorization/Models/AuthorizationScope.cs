namespace PermissionGraph.Domain.Authorization.Models;

public sealed record AuthorizationScope
{
    public AuthorizationScope(Guid organizationId, Guid? projectId = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                "authorization_organization_required",
                "Organization identifier is required for authorization scope.");
        }

        if (projectId == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                "authorization_project_invalid",
                "Project identifier cannot be empty when provided.");
        }

        OrganizationId = organizationId;
        ProjectId = projectId;
    }

    public Guid OrganizationId { get; }

    public Guid? ProjectId { get; }

    public AuthorizationScopeType ScopeType => ProjectId is null
        ? AuthorizationScopeType.Organization
        : AuthorizationScopeType.Project;
}
