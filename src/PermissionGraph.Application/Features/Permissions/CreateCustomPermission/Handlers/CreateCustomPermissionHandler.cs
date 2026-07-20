using FluentValidation;
using PermissionGraph.Application.Abstractions.Audit;
using PermissionGraph.Application.Abstractions.Clock;
using PermissionGraph.Application.Abstractions.Data;
using PermissionGraph.Application.Abstractions.Identifiers;
using PermissionGraph.Application.Abstractions.Organizations;
using PermissionGraph.Application.Abstractions.Permissions;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Errors;
using PermissionGraph.Application.Common.Validation;
using PermissionGraph.Domain.Common;
using PermissionGraph.Domain.Permissions;

namespace PermissionGraph.Application.Features.Permissions;

public sealed class CreateCustomPermissionHandler(
    IValidator<CreateCustomPermissionCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    PermissionCatalogAccess permissionCatalogAccess,
    IPermissionDefinitionRepository permissionRepository,
    IOrganizationPolicyVersionUpdater policyVersionUpdater,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IGuidProvider guidProvider,
    IClock clock)
{
    public async Task<PermissionResult> HandleAsync(
        CreateCustomPermissionCommand command,
        CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await permissionCatalogAccess.RequireOwnerActiveOrganizationAsync(
            command.OrganizationId,
            actor.UserId,
            cancellationToken);
        var key = command.Key.Trim();
        var normalizedKey = NormalizeKey(key);

        if (await permissionRepository.CustomNormalizedKeyExistsAsync(organization.Id, normalizedKey, excludingPermissionId: null, cancellationToken))
        {
            throw DuplicateName();
        }

        var now = clock.UtcNow;
        PermissionDefinition permission;
        try
        {
            permission = PermissionDefinition.CreateCustom(
                guidProvider.NewGuid(),
                organization.Id,
                key,
                normalizedKey,
                command.DisplayName,
                command.Description,
                command.Module,
                command.AllowedScopes,
                command.IsRequestable,
                now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        await permissionRepository.AddAsync(permission, cancellationToken);
        await policyVersionUpdater.IncrementPolicyVersionAsync(organization.Id, now, cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(organization.Id, actor.UserId, "permission.created", "PermissionDefinition", permission.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return PermissionResult.FromDomain(permission);
    }

    internal static string NormalizeKey(string key)
    {
        return key.Trim().ToLowerInvariant();
    }

    internal static ConflictApplicationException DuplicateName()
    {
        return new ConflictApplicationException("permission_key_already_exists", "A custom permission with this key already exists.");
    }
}
