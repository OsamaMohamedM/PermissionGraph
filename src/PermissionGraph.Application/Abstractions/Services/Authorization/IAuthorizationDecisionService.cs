namespace PermissionGraph.Application.Abstractions.Services.Authorization;

public interface IAuthorizationDecisionService
{
    Task<AuthorizationDecision> CheckAsync(
        CheckPermissionQuery query,
        CancellationToken cancellationToken);

    Task<BatchAuthorizationDecisionResult> BatchCheckAsync(
        BatchCheckPermissionsQuery query,
        CancellationToken cancellationToken);
}
