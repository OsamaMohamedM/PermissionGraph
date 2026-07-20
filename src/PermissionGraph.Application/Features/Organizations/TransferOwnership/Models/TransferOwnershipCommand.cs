namespace PermissionGraph.Application.Features.Organizations;

public sealed record TransferOwnershipCommand(Guid OrganizationId, Guid NewOwnerUserId, string CurrentPassword);
