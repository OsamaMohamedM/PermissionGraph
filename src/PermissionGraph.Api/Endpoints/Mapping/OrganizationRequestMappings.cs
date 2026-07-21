namespace PermissionGraph.Api.Endpoints.Mapping;

internal static class OrganizationRequestMappings
{
    public static CreateOrganizationCommand ToCommand(this CreateOrganizationRequest request)
    {
        return new CreateOrganizationCommand(request.Name, request.Description);
    }

    public static UpdateOrganizationCommand ToCommand(this UpdateOrganizationRequest request, Guid organizationId)
    {
        return new UpdateOrganizationCommand(organizationId, request.Name, request.Description);
    }

    public static ArchiveOrganizationCommand ToCommand(this ArchiveOrganizationRequest request, Guid organizationId)
    {
        return new ArchiveOrganizationCommand(organizationId, request.Confirmation);
    }

    public static TransferOwnershipCommand ToCommand(this TransferOwnershipRequest request, Guid organizationId)
    {
        return new TransferOwnershipCommand(organizationId, request.NewOwnerUserId, request.CurrentPassword);
    }

    public static AddOrganizationMemberCommand ToCommand(this AddOrganizationMemberRequest request, Guid organizationId)
    {
        return new AddOrganizationMemberCommand(organizationId, request.Email);
    }

    public static OrganizationResponse ToResponse(this OrganizationResult result)
    {
        return new OrganizationResponse(
            result.Id,
            result.Name,
            result.Description,
            result.OwnerUserId,
            result.Status.ToString(),
            result.CreatedAtUtc,
            result.UpdatedAtUtc);
    }

    public static OrganizationMemberResponse ToResponse(this OrganizationMemberResult result)
    {
        return new OrganizationMemberResponse(
            result.MembershipId,
            result.OrganizationId,
            result.UserId,
            result.Email,
            result.DisplayName,
            result.Status.ToString(),
            result.JoinedAtUtc,
            result.SuspendedAtUtc,
            result.RemovedAtUtc);
    }
}