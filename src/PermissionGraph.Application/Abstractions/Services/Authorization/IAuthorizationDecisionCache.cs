namespace PermissionGraph.Application.Abstractions.Services.Authorization;

public interface IAuthorizationDecisionCache
{
    Task<AuthorizationDecision?> GetAsync(
        AuthorizationDecisionCacheKey key,
        CancellationToken cancellationToken);

    Task SetAsync(
        AuthorizationDecisionCacheKey key,
        AuthorizationDecision decision,
        TimeSpan ttl,
        CancellationToken cancellationToken);
}

public sealed record AuthorizationDecisionCacheKey(
    Guid OrganizationId,
    long OrganizationPolicyVersion,
    Guid SubjectUserId,
    long SubjectAuthorizationVersion,
    AuthorizationScopeType ScopeType,
    Guid ScopeId,
    string PermissionNormalizedKey)
{
    public override string ToString()
    {
        return string.Join(
            ':',
            "authz",
            "v1",
            OrganizationId,
            OrganizationPolicyVersion,
            SubjectUserId,
            SubjectAuthorizationVersion,
            ScopeType,
            ScopeId,
            PermissionNormalizedKey);
    }
}

public sealed class NoOpAuthorizationDecisionCache : IAuthorizationDecisionCache
{
    public Task<AuthorizationDecision?> GetAsync(
        AuthorizationDecisionCacheKey key,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<AuthorizationDecision?>(null);
    }

    public Task SetAsync(
        AuthorizationDecisionCacheKey key,
        AuthorizationDecision decision,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
