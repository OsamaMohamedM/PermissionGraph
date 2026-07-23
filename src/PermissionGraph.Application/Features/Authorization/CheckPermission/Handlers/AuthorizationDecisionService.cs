namespace PermissionGraph.Application.Features.Authorization.CheckPermission.Handlers;

public sealed class AuthorizationDecisionService(
    IValidator<CheckPermissionQuery> checkValidator,
    IValidator<BatchCheckPermissionsQuery> batchValidator,
    ICurrentUser currentUser,
    IUserAccountLookup userAccountLookup,
    IAuthorizationReadService authorizationReadService,
    IClock clock) : IAuthorizationDecisionService
{
    public async Task<AuthorizationDecision> CheckAsync(
        CheckPermissionQuery query,
        CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(checkValidator, query, cancellationToken);

        var actor = await ResolveActorAsync(cancellationToken);
        if (actor.Decision is not null)
        {
            return actor.Decision;
        }

        var subject = await ResolveSubjectAsync(
            query.SubjectUserId,
            actor.User!.UserId,
            actor.User,
            cancellationToken);
        if (subject.Decision is not null)
        {
            return subject.Decision;
        }

        var readModel = await authorizationReadService.LoadEvaluationAsync(
            CreateReadRequest(query, subject.User!.UserId),
            cancellationToken);

        return EvaluateReadModel(readModel, actor.User, subject.User, query.SubjectUserId);
    }

    public async Task<BatchAuthorizationDecisionResult> BatchCheckAsync(
        BatchCheckPermissionsQuery query,
        CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(batchValidator, query, cancellationToken);

        var actor = await ResolveActorAsync(cancellationToken);
        if (actor.Decision is not null)
        {
            return DenyAll(query, actor.Decision);
        }

        var ordered = query.OrderedChecks;
        var subjects = new List<BatchSubjectResolution>(ordered.Count);
        var readRequests = new List<AuthorizationEvaluationReadRequest>();

        for (var index = 0; index < ordered.Count; index++)
        {
            var item = ordered[index];
            var subject = await ResolveSubjectAsync(
                item.SubjectUserId,
                actor.User!.UserId,
                actor.User,
                cancellationToken);

            subjects.Add(new BatchSubjectResolution(index, item, subject));
            if (subject.Decision is null)
            {
                readRequests.Add(CreateReadRequest(item.ToCheckPermissionQuery(), subject.User!.UserId));
            }
        }

        var readModels = readRequests.Count == 0
            ? []
            : await authorizationReadService.LoadBatchEvaluationAsync(readRequests, cancellationToken);
        var remaining = new Queue<AuthorizationEvaluationReadModel>(readModels);
        var decisions = new List<BatchAuthorizationDecision>(ordered.Count);

        foreach (var subject in subjects)
        {
            var decision = subject.Resolution.Decision
                ?? EvaluateReadModel(remaining.Dequeue(), actor.User!, subject.Resolution.User!, subject.Item.SubjectUserId);

            decisions.Add(new BatchAuthorizationDecision(subject.Item.CorrelationId, subject.Index, decision));
        }

        return new BatchAuthorizationDecisionResult(decisions);
    }

    private async Task<UserResolution> ResolveActorAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            return new UserResolution(null, Deny(AuthorizationReasonCode.DeniedUnauthenticated));
        }

        var actor = await userAccountLookup.FindByIdAsync(currentUser.UserId.Value, cancellationToken);
        if (actor is null || !actor.IsActive)
        {
            return new UserResolution(null, Deny(AuthorizationReasonCode.DeniedActorInactive));
        }

        return new UserResolution(actor, null);
    }

    private async Task<UserResolution> ResolveSubjectAsync(
        Guid? requestedSubjectUserId,
        Guid actorUserId,
        UserAccount actor,
        CancellationToken cancellationToken)
    {
        var subjectUserId = requestedSubjectUserId ?? actorUserId;
        var subject = subjectUserId == actorUserId
            ? actor
            : await userAccountLookup.FindByIdAsync(subjectUserId, cancellationToken);

        if (subject is null || !subject.IsActive)
        {
            return new UserResolution(null, Deny(AuthorizationReasonCode.DeniedSubjectInactive));
        }

        return new UserResolution(subject, null);
    }

    private AuthorizationDecision EvaluateReadModel(
        AuthorizationEvaluationReadModel readModel,
        UserAccount actor,
        UserAccount subject,
        Guid? requestedSubjectUserId)
    {
        var organization = readModel.Organization;
        if (organization is null || !organization.IsActive)
        {
            return Deny(AuthorizationReasonCode.DeniedOrganizationNotFoundOrInactive);
        }

        var permission = readModel.Permission;
        if (!IsVisibleActivePermission(permission, readModel.Request.OrganizationId))
        {
            return Deny(AuthorizationReasonCode.DeniedPermissionNotFoundOrInactive);
        }

        if (readModel.Request.ProjectId is not null)
        {
            var project = readModel.Project;
            if (project is null || !project.IsActive)
            {
                return Deny(AuthorizationReasonCode.DeniedProjectNotFoundOrInactive);
            }

            if (project.OrganizationId != readModel.Request.OrganizationId)
            {
                return Deny(AuthorizationReasonCode.DeniedProjectOutsideOrganization);
            }
        }

        var scope = new AuthorizationScope(readModel.Request.OrganizationId, readModel.Request.ProjectId);
        if (!IsPermissionScopeCompatible(scope.ScopeType, permission!.AllowedScopes))
        {
            return Deny(AuthorizationReasonCode.DeniedScopeMismatch);
        }

        var isOtherUserCheck = requestedSubjectUserId is not null && requestedSubjectUserId.Value != actor.UserId;
        if (isOtherUserCheck && organization.OwnerUserId != actor.UserId)
        {
            return Deny(AuthorizationReasonCode.DeniedCheckOtherUsersNotAllowed);
        }

        if (organization.OwnerUserId != subject.UserId &&
            readModel.SubjectMembership?.IsActive != true)
        {
            return Deny(AuthorizationReasonCode.DeniedMembershipNotActive);
        }

        if (organization.OwnerUserId == subject.UserId)
        {
            return Allow(AuthorizationReasonCode.AllowedOwnerOverride);
        }

        if (readModel.Request.ProjectId is not null &&
            HasProjectAdministratorPermissionPath(readModel, permission))
        {
            return Allow(AuthorizationReasonCode.AllowedRolePermissionMatch);
        }

        // M07 RoleAssignments and M08 DirectGrants will add paths here without changing fail-closed defaults.
        return Deny(AuthorizationReasonCode.DeniedNoApplicableGrant);
    }

    private static bool IsVisibleActivePermission(
        AuthorizationPermissionReadModel? permission,
        Guid organizationId)
    {
        return permission is not null &&
            permission.IsActive &&
            string.Equals(permission.NormalizedKey, permission.NormalizedKey.Trim().ToLowerInvariant(), StringComparison.Ordinal) &&
            (permission.PermissionType == PermissionType.Platform || permission.OrganizationId == organizationId);
    }

    private static bool HasProjectAdministratorPermissionPath(
        AuthorizationEvaluationReadModel readModel,
        AuthorizationPermissionReadModel permission)
    {
        return readModel.ProjectAdministratorPermissionPaths.Any(path =>
            path.AssignmentOrganizationId == readModel.Request.OrganizationId &&
            path.AssignmentProjectId == readModel.Request.ProjectId &&
            path.AssignmentUserId == readModel.Request.SubjectUserId &&
            path.RoleIsActive &&
            path.RoleScopeType == RoleScopeType.Project &&
            path.PermissionId == permission.Id &&
            path.PermissionIsActive &&
            string.Equals(path.PermissionNormalizedKey, readModel.Request.NormalizedPermissionKey, StringComparison.Ordinal) &&
            IsPermissionScopeCompatible(AuthorizationScopeType.Project, path.PermissionAllowedScopes));
    }

    private static bool IsPermissionScopeCompatible(
        AuthorizationScopeType requestedScope,
        PermissionAllowedScopes allowedScopes)
    {
        return requestedScope switch
        {
            AuthorizationScopeType.Organization => allowedScopes is PermissionAllowedScopes.Organization or PermissionAllowedScopes.OrganizationAndProject,
            AuthorizationScopeType.Project => allowedScopes is PermissionAllowedScopes.Project or PermissionAllowedScopes.OrganizationAndProject,
            _ => false
        };
    }

    private AuthorizationDecision Allow(string reasonCode)
    {
        return AuthorizationDecision.Allow(reasonCode, clock.UtcNow);
    }

    private AuthorizationDecision Deny(string reasonCode)
    {
        return AuthorizationDecision.Deny(reasonCode, clock.UtcNow);
    }

    private static AuthorizationEvaluationReadRequest CreateReadRequest(
        CheckPermissionQuery query,
        Guid subjectUserId)
    {
        return new AuthorizationEvaluationReadRequest(
            subjectUserId,
            query.OrganizationId,
            query.ProjectId,
            query.NormalizedPermissionKey);
    }

    private static BatchAuthorizationDecisionResult DenyAll(
        BatchCheckPermissionsQuery query,
        AuthorizationDecision decision)
    {
        var decisions = query.OrderedChecks
            .Select((item, index) => new BatchAuthorizationDecision(item.CorrelationId, index, decision))
            .ToArray();

        return new BatchAuthorizationDecisionResult(decisions);
    }

    private sealed record UserResolution(
        UserAccount? User,
        AuthorizationDecision? Decision);

    private sealed record BatchSubjectResolution(
        int Index,
        BatchCheckPermissionItem Item,
        UserResolution Resolution);
}
