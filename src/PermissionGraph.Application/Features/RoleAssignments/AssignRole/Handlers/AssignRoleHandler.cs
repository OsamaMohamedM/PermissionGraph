namespace PermissionGraph.Application.Features.RoleAssignments.AssignRole.Handlers;

public sealed class AssignRoleHandler(
    IValidator<AssignRoleCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    IOrganizationRepository organizationRepository,
    IOrganizationMembershipRepository membershipRepository,
    IProjectRepository projectRepository,
    IRoleRepository roleRepository,
    IPermissionDefinitionRepository permissionRepository,
    IRoleAssignmentRepository assignmentRepository,
    IAuthorizationDecisionService authorizationDecisionService,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IGuidProvider guidProvider,
    IClock clock)
{
    public async Task<RoleAssignmentResult> HandleAsync(
        AssignRoleCommand command,
        CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await RequireActiveOrganizationAsync(command.OrganizationId, cancellationToken);
        var targetMembership = await RequireActiveTargetMembershipAsync(command.OrganizationId, command.UserId, cancellationToken);
        var role = await RequireAssignableRoleAsync(command.OrganizationId, command.RoleId, cancellationToken);

        await EnsureScopeMatchesRoleAsync(command, role, cancellationToken);

        var actorIsOwner = organization.OwnerUserId == actor.UserId;
        if (!actorIsOwner)
        {
            await RequireActiveActorMembershipAsync(command.OrganizationId, actor.UserId, cancellationToken);

            if (command.UserId == actor.UserId)
            {
                await PersistPrivilegeEscalationDeniedAuditAsync(command.OrganizationId, actor.UserId, command.RoleId, cancellationToken);
                throw new ForbiddenApplicationException(
                    "role_assignment_self_assignment_denied",
                    "A non-owner cannot assign a role to themselves.");
            }

            await RequirePermissionAsync(actor.UserId, command, "pg.roles.assign", cancellationToken);
            await RequireCanGrantRolePermissionsAsync(actor.UserId, command, role, cancellationToken);
        }

        if (await assignmentRepository.HasEffectiveAssignmentAsync(
                command.OrganizationId,
                command.UserId,
                command.RoleId,
                command.ScopeType,
                command.ScopeId,
                cancellationToken))
        {
            throw new ConflictApplicationException(
                "role_assignment_duplicate_effective",
                "An active or scheduled role assignment already exists for this user, role, and scope.");
        }

        var now = clock.UtcNow;
        RoleAssignment assignment;
        try
        {
            assignment = RoleAssignment.Create(
                guidProvider.NewGuid(),
                organization.Id,
                targetMembership.UserId,
                role.Id,
                command.ScopeType,
                command.ScopeId,
                command.StartsAtUtc,
                command.ExpiresAtUtc,
                actor.UserId,
                command.Reason,
                now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        await assignmentRepository.AddAsync(assignment, cancellationToken);
        await membershipRepository.IncrementAuthorizationVersionAsync(
            assignment.OrganizationId,
            assignment.UserId,
            now,
            cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(assignment.OrganizationId, actor.UserId, "role_assignment.created", "RoleAssignment", assignment.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return RoleAssignmentResult.FromDomain(assignment);
    }

    private async Task<Organization> RequireActiveOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId, cancellationToken);
        return organization is not null && organization.IsActive
            ? organization
            : throw OrganizationAccessHelper.NotFound();
    }

    private async Task<OrganizationMembership> RequireActiveTargetMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await membershipRepository.GetByOrganizationAndUserAsync(
            organizationId,
            userId,
            cancellationToken);

        if (membership is null || !membership.IsActive)
        {
            throw new NotFoundApplicationException(
                "organization_member_not_found",
                "Organization member could not be found.");
        }

        return membership;
    }

    private async Task RequireActiveActorMembershipAsync(
        Guid organizationId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var membership = await membershipRepository.GetByOrganizationAndUserAsync(
            organizationId,
            actorUserId,
            cancellationToken);

        if (membership is null || !membership.IsActive)
        {
            throw OrganizationAccessHelper.NotFound();
        }
    }

    private async Task<Role> RequireAssignableRoleAsync(
        Guid organizationId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var role = await roleRepository.GetVisibleByOrganizationAndIdAsync(
            organizationId,
            roleId,
            cancellationToken);

        if (role is null)
        {
            throw RoleCatalogAccessHelper.NotFound();
        }

        if (!role.IsActive)
        {
            throw new ConflictApplicationException(
                "role_assignment_role_inactive",
                "Inactive role cannot receive new assignments.");
        }

        return role;
    }

    private async Task EnsureScopeMatchesRoleAsync(
        AssignRoleCommand command,
        Role role,
        CancellationToken cancellationToken)
    {
        var expectedScope = ToAssignmentScopeType(role.ScopeType);
        if (command.ScopeType != expectedScope)
        {
            throw new ConflictApplicationException(
                "role_assignment_scope_mismatch",
                "Role scope must match assignment scope.");
        }

        if (command.ScopeType == RoleAssignmentScopeType.Organization)
        {
            if (command.ScopeId != command.OrganizationId)
            {
                throw new ConflictApplicationException(
                    "role_assignment_organization_scope_mismatch",
                    "Organization role assignment scope must match the organization.");
            }

            return;
        }

        var project = await projectRepository.GetByOrganizationAndIdAsync(
            command.OrganizationId,
            command.ScopeId,
            cancellationToken);

        if (project is null || !project.IsActive)
        {
            throw ProjectAccessHelper.NotFound();
        }
    }

    private async Task RequireCanGrantRolePermissionsAsync(
        Guid actorUserId,
        AssignRoleCommand command,
        Role role,
        CancellationToken cancellationToken)
    {
        var rolePermissionIds = role.Permissions
            .Select(rolePermission => rolePermission.PermissionId)
            .Distinct()
            .ToArray();

        if (rolePermissionIds.Length == 0)
        {
            return;
        }

        var permissions = await permissionRepository.ListVisibleByOrganizationAndIdsAsync(
            command.OrganizationId,
            rolePermissionIds,
            cancellationToken);
        var permissionsById = permissions.ToDictionary(permission => permission.Id);

        foreach (var permissionId in rolePermissionIds)
        {
            if (!permissionsById.TryGetValue(permissionId, out var permission) || !permission.IsActive)
            {
                throw new ConflictApplicationException(
                    "role_assignment_permission_inactive",
                    "Role contains an inactive permission and cannot be assigned.");
            }
        }

        var grantabilityChecks = CreateGrantabilityChecks(actorUserId, command, rolePermissionIds, permissionsById);
        var grantabilityDecisions = await BatchCheckGrantabilityAsync(
            grantabilityChecks.SelectMany(check => check.AuthorizationChecks).ToArray(),
            cancellationToken);

        foreach (var grantabilityCheck in grantabilityChecks)
        {
            var targetAllowed = grantabilityDecisions.GetValueOrDefault(grantabilityCheck.TargetCorrelationId);
            var broaderAllowed = grantabilityCheck.BroaderCorrelationId is not null &&
                grantabilityDecisions.GetValueOrDefault(grantabilityCheck.BroaderCorrelationId);

            if (!targetAllowed && !broaderAllowed)
            {
                await PersistPrivilegeEscalationDeniedAuditAsync(command.OrganizationId, actorUserId, role.Id, cancellationToken);
                throw new ForbiddenApplicationException(
                    "role_assignment_grantability_denied",
                    "Actor cannot assign a role containing permissions they do not possess.");
            }
        }
    }

    private static IReadOnlyList<PermissionGrantabilityCheck> CreateGrantabilityChecks(
        Guid actorUserId,
        AssignRoleCommand command,
        IReadOnlyList<Guid> rolePermissionIds,
        IReadOnlyDictionary<Guid, PermissionDefinition> permissionsById)
    {
        var targetScopeProjectId = command.ScopeType == RoleAssignmentScopeType.Project
            ? command.ScopeId
            : (Guid?)null;

        return rolePermissionIds
            .Select(permissionId =>
            {
                var permission = permissionsById[permissionId];
                var targetCorrelationId = $"target:{permissionId:N}";
                var checks = new List<BatchCheckPermissionItem>
                {
                    new(targetCorrelationId, actorUserId, command.OrganizationId, targetScopeProjectId, permission.Key)
                };
                string? broaderCorrelationId = null;

                if (command.ScopeType == RoleAssignmentScopeType.Project)
                {
                    broaderCorrelationId = $"broader:{permissionId:N}";
                    checks.Add(new BatchCheckPermissionItem(
                        broaderCorrelationId,
                        actorUserId,
                        command.OrganizationId,
                        null,
                        permission.Key));
                }

                return new PermissionGrantabilityCheck(permissionId, targetCorrelationId, broaderCorrelationId, checks);
            })
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<string, bool>> BatchCheckGrantabilityAsync(
        IReadOnlyList<BatchCheckPermissionItem> checks,
        CancellationToken cancellationToken)
    {
        var decisions = new Dictionary<string, bool>(StringComparer.Ordinal);

        for (var index = 0; index < checks.Count; index += BatchCheckPermissionsQuery.MaxChecks)
        {
            var chunk = checks
                .Skip(index)
                .Take(BatchCheckPermissionsQuery.MaxChecks)
                .ToArray();
            var result = await authorizationDecisionService.BatchCheckAsync(
                new BatchCheckPermissionsQuery(chunk),
                cancellationToken);

            foreach (var item in result.Items)
            {
                decisions[item.CorrelationId] = item.Decision.Allowed;
            }
        }

        return decisions;
    }

    private async Task RequirePermissionAsync(
        Guid actorUserId,
        AssignRoleCommand command,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var projectId = command.ScopeType == RoleAssignmentScopeType.Project
            ? command.ScopeId
            : (Guid?)null;
        var decision = await authorizationDecisionService.CheckAsync(
            new CheckPermissionQuery(actorUserId, command.OrganizationId, projectId, permissionKey),
            cancellationToken);

        if (!decision.Allowed)
        {
            throw new ForbiddenApplicationException(
                "role_assignment_not_authorized",
                "Actor is not allowed to assign roles in this scope.");
        }
    }

    private async Task PersistPrivilegeEscalationDeniedAuditAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(organizationId, actorUserId, "role_assignment.privilege_escalation_denied", "Role", roleId, "Failed", clock.UtcNow),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }

    private sealed record PermissionGrantabilityCheck(
        Guid PermissionId,
        string TargetCorrelationId,
        string? BroaderCorrelationId,
        IReadOnlyList<BatchCheckPermissionItem> AuthorizationChecks);

    private static RoleAssignmentScopeType ToAssignmentScopeType(RoleScopeType roleScopeType)
    {
        return roleScopeType switch
        {
            RoleScopeType.Organization => RoleAssignmentScopeType.Organization,
            RoleScopeType.Project => RoleAssignmentScopeType.Project,
            _ => throw new ConflictApplicationException("role_assignment_scope_mismatch", "Role scope must match assignment scope.")
        };
    }
}
