namespace PermissionGraph.Application.Features.Organizations.TransferOwnership.Models;

public sealed record TransferOwnershipCommand(Guid OrganizationId, Guid NewOwnerUserId, string CurrentPassword);