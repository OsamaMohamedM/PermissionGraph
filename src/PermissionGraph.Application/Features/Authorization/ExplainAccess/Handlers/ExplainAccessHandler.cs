namespace PermissionGraph.Application.Features.Authorization.ExplainAccess.Handlers;

public sealed class ExplainAccessHandler(
    IValidator<ExplainAccessQuery> validator,
    ICurrentUser currentUser,
    IUserAccountLookup userAccountLookup,
    IAuthorizationDecisionService authorizationDecisionService,
    IAccessExplanationReadService explanationReadService,
    IApplicationTransaction transaction,
    IAuditWriter auditWriter,
    IClock clock)
{
    private const string ExplainOthersPermissionKey = "pg.authorization.explain_others";

    public async Task<ExplainAccessResult> HandleAsync(
        ExplainAccessQuery query,
        CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);

        var actor = await RequireActiveActorAsync(cancellationToken);
        var subjectUserId = query.SubjectUserId ?? actor.UserId;
        var isOtherUser = subjectUserId != actor.UserId;

        var readModel = await explanationReadService.LoadAsync(
            new AccessExplanationReadRequest(
                subjectUserId,
                query.OrganizationId,
                query.ProjectId,
                query.NormalizedPermissionKey),
            cancellationToken);

        if (isOtherUser)
        {
            if (readModel.Organization?.OwnerUserId != actor.UserId &&
                !await ActorCanExplainOtherUserAsync(query, cancellationToken))
            {
                await PersistExplainOtherAuditAsync(query.OrganizationId, actor.UserId, subjectUserId, "Failed", cancellationToken);
                throw new ForbiddenApplicationException(
                    "access_explanation_other_user_denied",
                    "Actor is not allowed to explain another user's access.");
            }
        }

        var decision = await authorizationDecisionService.CheckAsync(
            new CheckPermissionQuery(query.SubjectUserId, query.OrganizationId, query.ProjectId, query.PermissionKey),
            cancellationToken);

        if (isOtherUser)
        {
            await PersistExplainOtherAuditAsync(query.OrganizationId, actor.UserId, subjectUserId, "Succeeded", cancellationToken);
        }

        var steps = BuildSteps(readModel, actor.UserId, subjectUserId, decision);
        var matchedPath = decision.Allowed ? FindMatchedPath(readModel, decision.EvaluatedAtUtc, decision.ReasonCode) : null;
        var scope = new AuthorizationScope(query.OrganizationId, query.ProjectId);

        return new ExplainAccessResult(
            decision.Allowed,
            decision.ReasonCode,
            decision.EvaluatedAtUtc,
            actor.UserId,
            subjectUserId,
            query.OrganizationId,
            query.ProjectId,
            query.NormalizedPermissionKey,
            scope.ScopeType,
            CreateSummary(decision),
            steps,
            matchedPath);
    }

    private async Task<bool> ActorCanExplainOtherUserAsync(
        ExplainAccessQuery query,
        CancellationToken cancellationToken)
    {
        var decision = await authorizationDecisionService.CheckAsync(
            new CheckPermissionQuery(null, query.OrganizationId, query.ProjectId, ExplainOthersPermissionKey),
            cancellationToken);

        return decision.Allowed;
    }

    private async Task<UserAccount> RequireActiveActorAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            throw new UnauthorizedApplicationException(
                "unauthenticated",
                "Authentication is required.");
        }

        var actor = await userAccountLookup.FindByIdAsync(currentUser.UserId.Value, cancellationToken);
        if (actor is null || !actor.IsActive)
        {
            throw new ForbiddenApplicationException(
                "actor_inactive",
                "Actor account is inactive.");
        }

        return actor;
    }

    private async Task PersistExplainOtherAuditAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid subjectUserId,
        string result,
        CancellationToken cancellationToken)
    {
        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(
                organizationId,
                actorUserId,
                "authorization.explain_other",
                "User",
                subjectUserId,
                result,
                clock.UtcNow),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }

    private static IReadOnlyList<AccessExplanationStepResult> BuildSteps(
        AccessExplanationReadModel readModel,
        Guid actorUserId,
        Guid subjectUserId,
        AuthorizationDecision decision)
    {
        var steps = new List<AccessExplanationStepResult>();
        var order = 1;

        Add(steps, ref order, "ACTOR_AUTHENTICATED", AccessExplanationStepStatus.Passed, "Actor is authenticated.");
        Add(steps, ref order, "ACTOR_ACTIVE", AccessExplanationStepStatus.Passed, "Actor account is active.");
        Add(steps, ref order, "SUBJECT_RESOLVED", AccessExplanationStepStatus.Passed, "Subject user was resolved.", new Dictionary<string, string>
        {
            ["subjectUserId"] = subjectUserId.ToString()
        });

        var organization = readModel.Organization;
        if (organization is null || !organization.IsActive)
        {
            Add(steps, ref order, "ORGANIZATION_ACTIVE", AccessExplanationStepStatus.Failed, "Organization is missing or inactive.");
            AddFinal(steps, ref order, decision);
            return steps;
        }

        Add(steps, ref order, "ORGANIZATION_ACTIVE", AccessExplanationStepStatus.Passed, "Organization exists and is active.");

        var permission = readModel.Permission;
        if (permission is null || !permission.IsActive)
        {
            Add(steps, ref order, "PERMISSION_ACTIVE", AccessExplanationStepStatus.Failed, "Permission is missing or inactive.");
            AddFinal(steps, ref order, decision);
            return steps;
        }

        Add(steps, ref order, "PERMISSION_ACTIVE", AccessExplanationStepStatus.Passed, "Permission exists and is active.");

        if (readModel.Request.ProjectId is not null)
        {
            var project = readModel.Project;
            if (project is null || !project.IsActive)
            {
                Add(steps, ref order, "PROJECT_ACTIVE", AccessExplanationStepStatus.Failed, "Project is missing or inactive.");
                AddFinal(steps, ref order, decision);
                return steps;
            }

            if (project.OrganizationId != readModel.Request.OrganizationId)
            {
                Add(steps, ref order, "PROJECT_TENANT_MATCH", AccessExplanationStepStatus.Failed, "Project does not belong to the organization.");
                AddFinal(steps, ref order, decision);
                return steps;
            }

            Add(steps, ref order, "PROJECT_ACTIVE", AccessExplanationStepStatus.Passed, "Project exists, is active, and belongs to the organization.");
        }
        else
        {
            Add(steps, ref order, "PROJECT_ACTIVE", AccessExplanationStepStatus.Skipped, "No project scope was requested.");
        }

        var requestedScopeType = readModel.Request.ProjectId is null
            ? AuthorizationScopeType.Organization
            : AuthorizationScopeType.Project;
        if (!IsPermissionScopeCompatible(requestedScopeType, permission.AllowedScopes))
        {
            Add(steps, ref order, "SCOPE_COMPATIBLE", AccessExplanationStepStatus.Failed, "Permission is not compatible with the requested scope.");
            AddFinal(steps, ref order, decision);
            return steps;
        }

        Add(steps, ref order, "SCOPE_COMPATIBLE", AccessExplanationStepStatus.Passed, "Permission is compatible with the requested scope.");

        if (organization.OwnerUserId != subjectUserId && readModel.SubjectMembership?.IsActive != true)
        {
            Add(steps, ref order, "MEMBERSHIP_ACTIVE", AccessExplanationStepStatus.Failed, "Subject is not an active member.");
            AddFinal(steps, ref order, decision);
            return steps;
        }

        Add(steps, ref order, "MEMBERSHIP_ACTIVE", AccessExplanationStepStatus.Passed, "Subject owner override or active membership is present.");

        if (organization.OwnerUserId == subjectUserId)
        {
            Add(steps, ref order, "OWNER_OVERRIDE", AccessExplanationStepStatus.Passed, "Subject is the organization owner.");
            AddFinal(steps, ref order, decision);
            return steps;
        }

        Add(steps, ref order, "OWNER_OVERRIDE", AccessExplanationStepStatus.Info, "Subject is not the organization owner.");

        AddRoleAssignmentSteps(steps, ref order, readModel, decision.EvaluatedAtUtc);
        AddProjectAdministratorSteps(steps, ref order, readModel);
        AddFinal(steps, ref order, decision);
        return steps;
    }

    private static void AddRoleAssignmentSteps(
        List<AccessExplanationStepResult> steps,
        ref int order,
        AccessExplanationReadModel readModel,
        DateTimeOffset evaluatedAtUtc)
    {
        if (readModel.RoleAssignments.Count == 0)
        {
            Add(steps, ref order, "ROLE_ASSIGNMENTS_CHECKED", AccessExplanationStepStatus.Info, "No role assignments were found for this subject and organization.");
            return;
        }

        foreach (var assignment in readModel.RoleAssignments.OrderBy(item => item.AssignmentStartsAtUtc))
        {
            var details = new Dictionary<string, string>
            {
                ["assignmentId"] = assignment.AssignmentId.ToString(),
                ["roleId"] = assignment.AssignmentRoleId.ToString(),
                ["roleName"] = assignment.RoleName,
                ["assignmentStatus"] = assignment.AssignmentStatus.ToString(),
                ["assignmentScopeType"] = assignment.AssignmentScopeType.ToString(),
                ["assignmentScopeId"] = assignment.AssignmentScopeId.ToString()
            };

            if (assignment.AssignmentStatus == RoleAssignmentStatus.Revoked)
            {
                Add(steps, ref order, "ROLE_ASSIGNMENT_REVOKED", AccessExplanationStepStatus.Info, "A role assignment exists but is revoked.", details);
                continue;
            }

            if (assignment.AssignmentStatus == RoleAssignmentStatus.Expired ||
                (assignment.AssignmentExpiresAtUtc is not null && evaluatedAtUtc >= assignment.AssignmentExpiresAtUtc.Value))
            {
                Add(steps, ref order, "ROLE_ASSIGNMENT_EXPIRED", AccessExplanationStepStatus.Info, "A role assignment exists but is expired.", details);
                continue;
            }

            if (assignment.AssignmentStartsAtUtc > evaluatedAtUtc)
            {
                Add(steps, ref order, "ROLE_ASSIGNMENT_NOT_STARTED", AccessExplanationStepStatus.Info, "A role assignment exists but has not started.", details);
                continue;
            }

            if (!assignment.RoleIsActive)
            {
                Add(steps, ref order, "ROLE_INACTIVE", AccessExplanationStepStatus.Info, "A role assignment exists but the role is inactive.", details);
                continue;
            }

            if (!assignment.RoleContainsPermission)
            {
                Add(steps, ref order, "ROLE_PERMISSION_MISSING", AccessExplanationStepStatus.Info, "A role assignment exists but the role does not contain the requested permission.", details);
                continue;
            }

            if (assignment.MatchedPermissionIsActive != true)
            {
                Add(steps, ref order, "ROLE_PERMISSION_INACTIVE", AccessExplanationStepStatus.Info, "A role assignment contains the permission, but that permission is inactive.", details);
                continue;
            }

            var requestedScopeType = readModel.Request.ProjectId is null
                ? RoleAssignmentScopeType.Organization
                : RoleAssignmentScopeType.Project;
            var requestedScopeId = readModel.Request.ProjectId ?? readModel.Request.OrganizationId;
            if (assignment.AssignmentScopeType != requestedScopeType || assignment.AssignmentScopeId != requestedScopeId)
            {
                Add(steps, ref order, "ROLE_ASSIGNMENT_SCOPE_MISMATCH", AccessExplanationStepStatus.Info, "A role assignment exists but does not match the requested scope.", details);
                continue;
            }

            Add(steps, ref order, "ROLE_ASSIGNMENT_MATCHED", AccessExplanationStepStatus.Passed, "An effective role assignment grants the requested permission.", details);
        }
    }

    private static void AddProjectAdministratorSteps(
        List<AccessExplanationStepResult> steps,
        ref int order,
        AccessExplanationReadModel readModel)
    {
        if (readModel.Request.ProjectId is null)
        {
            Add(steps, ref order, "PROJECT_ADMINISTRATOR_ASSIGNMENTS_CHECKED", AccessExplanationStepStatus.Skipped, "Project administrator compatibility path is only evaluated for project scope.");
            return;
        }

        if (readModel.ProjectAdministratorAssignments.Count == 0)
        {
            Add(steps, ref order, "PROJECT_ADMINISTRATOR_ASSIGNMENTS_CHECKED", AccessExplanationStepStatus.Info, "No matching project administrator compatibility assignment was found.");
            return;
        }

        foreach (var assignment in readModel.ProjectAdministratorAssignments)
        {
            var details = new Dictionary<string, string>
            {
                ["roleId"] = assignment.RoleId.ToString(),
                ["roleName"] = assignment.RoleName,
                ["projectId"] = assignment.AssignmentProjectId.ToString()
            };

            if (assignment.RoleIsActive && assignment.RoleContainsPermission && assignment.MatchedPermissionIsActive == true)
            {
                Add(steps, ref order, "PROJECT_ADMINISTRATOR_PATH_MATCHED", AccessExplanationStepStatus.Passed, "Project administrator compatibility path grants the requested permission.", details);
            }
            else
            {
                Add(steps, ref order, "PROJECT_ADMINISTRATOR_PATH_NOT_MATCHED", AccessExplanationStepStatus.Info, "Project administrator compatibility path did not grant the requested permission.", details);
            }
        }
    }

    private static AccessExplanationPathResult? FindMatchedPath(
        AccessExplanationReadModel readModel,
        DateTimeOffset evaluatedAtUtc,
        string reasonCode)
    {
        if (reasonCode == AuthorizationReasonCode.AllowedOwnerOverride)
        {
            return new AccessExplanationPathResult(
                "OwnerOverride",
                null,
                null,
                null,
                AuthorizationScopeType.Organization.ToString(),
                readModel.Request.OrganizationId,
                null,
                null);
        }

        if (reasonCode != AuthorizationReasonCode.AllowedRolePermissionMatch)
        {
            return null;
        }

        var roleAssignment = readModel.RoleAssignments.FirstOrDefault(assignment =>
            assignment.AssignmentStatus is RoleAssignmentStatus.Active or RoleAssignmentStatus.Scheduled &&
            assignment.AssignmentStartsAtUtc <= evaluatedAtUtc &&
            (assignment.AssignmentExpiresAtUtc is null || evaluatedAtUtc < assignment.AssignmentExpiresAtUtc.Value) &&
            assignment.RoleIsActive &&
            assignment.RoleContainsPermission &&
            assignment.MatchedPermissionIsActive == true &&
            AssignmentScopeMatches(readModel, assignment));

        if (roleAssignment is not null)
        {
            return new AccessExplanationPathResult(
                "RoleAssignment",
                roleAssignment.AssignmentId,
                roleAssignment.AssignmentRoleId,
                roleAssignment.RoleName,
                roleAssignment.AssignmentScopeType.ToString(),
                roleAssignment.AssignmentScopeId,
                roleAssignment.AssignmentStartsAtUtc,
                roleAssignment.AssignmentExpiresAtUtc);
        }

        var projectAdministrator = readModel.ProjectAdministratorAssignments.FirstOrDefault(path =>
            path.RoleIsActive &&
            path.RoleContainsPermission &&
            path.MatchedPermissionIsActive == true &&
            path.AssignmentProjectId == readModel.Request.ProjectId);

        return projectAdministrator is null
            ? null
            : new AccessExplanationPathResult(
                "ProjectAdministratorAssignment",
                null,
                projectAdministrator.RoleId,
                projectAdministrator.RoleName,
                RoleAssignmentScopeType.Project.ToString(),
                projectAdministrator.AssignmentProjectId,
                null,
                null);
    }

    private static bool AssignmentScopeMatches(
        AccessExplanationReadModel readModel,
        AccessExplanationRoleAssignmentReadModel assignment)
    {
        var requestedScopeType = readModel.Request.ProjectId is null
            ? RoleAssignmentScopeType.Organization
            : RoleAssignmentScopeType.Project;
        var requestedScopeId = readModel.Request.ProjectId ?? readModel.Request.OrganizationId;

        return assignment.AssignmentScopeType == requestedScopeType &&
            assignment.AssignmentScopeId == requestedScopeId;
    }

    private static void AddFinal(
        List<AccessExplanationStepResult> steps,
        ref int order,
        AuthorizationDecision decision)
    {
        Add(
            steps,
            ref order,
            "FINAL_DECISION",
            decision.Allowed ? AccessExplanationStepStatus.Passed : AccessExplanationStepStatus.Failed,
            decision.Allowed ? "Final authorization decision is allowed." : "Final authorization decision is denied.",
            new Dictionary<string, string>
            {
                ["reasonCode"] = decision.ReasonCode
            });
    }

    private static void Add(
        List<AccessExplanationStepResult> steps,
        ref int order,
        string code,
        string status,
        string message,
        IReadOnlyDictionary<string, string>? details = null)
    {
        steps.Add(new AccessExplanationStepResult(
            order++,
            code,
            status,
            message,
            details ?? new Dictionary<string, string>()));
    }

    private static string CreateSummary(AuthorizationDecision decision)
    {
        return decision.Allowed
            ? $"Access is allowed because {decision.ReasonCode} matched."
            : $"Access is denied because {decision.ReasonCode} matched.";
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
}
