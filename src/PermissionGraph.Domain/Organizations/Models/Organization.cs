using PermissionGraph.Domain.Common;

namespace PermissionGraph.Domain.Organizations;

public sealed class Organization
{
    public const int NameMinLength = 3;
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 1000;

    private Organization(
        Guid id,
        string name,
        string normalizedName,
        string? description,
        Guid ownerUserId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
        OwnerUserId = ownerUserId;
        Status = OrganizationStatus.Active;
        PolicyVersion = 1;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    private Organization()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public string? Description { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public OrganizationStatus Status { get; private set; }

    public long PolicyVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public bool IsActive => Status == OrganizationStatus.Active;

    public static Organization Create(
        Guid id,
        string name,
        string normalizedName,
        string? description,
        Guid ownerUserId,
        DateTimeOffset createdAtUtc)
    {
        EnsureNotEmpty(id, nameof(id));
        EnsureNotEmpty(ownerUserId, nameof(ownerUserId));
        EnsureName(name);
        EnsureNormalizedName(normalizedName);
        EnsureDescription(description);

        return new Organization(id, name, normalizedName, description, ownerUserId, createdAtUtc);
    }

    public void UpdateDetails(
        string name,
        string normalizedName,
        string? description,
        DateTimeOffset updatedAtUtc)
    {
        EnsureActive("archived_organization_cannot_be_updated", "Archived organization cannot be updated.");
        EnsureName(name);
        EnsureNormalizedName(normalizedName);
        EnsureDescription(description);

        Name = name;
        NormalizedName = normalizedName;
        Description = description;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Archive(DateTimeOffset archivedAtUtc)
    {
        EnsureActive("organization_already_archived", "Archived organization cannot be archived again.");

        Status = OrganizationStatus.Archived;
        UpdatedAtUtc = archivedAtUtc;
    }

    public void TransferOwnership(Guid newOwnerUserId, DateTimeOffset transferredAtUtc)
    {
        EnsureActive("archived_organization_cannot_transfer_ownership", "Archived organization cannot transfer ownership.");
        EnsureNotEmpty(newOwnerUserId, nameof(newOwnerUserId));

        if (newOwnerUserId == OwnerUserId)
        {
            throw new DomainRuleViolationException(
                "ownership_transfer_to_current_owner",
                "Ownership cannot transfer to the current owner.");
        }

        OwnerUserId = newOwnerUserId;
        PolicyVersion++;
        UpdatedAtUtc = transferredAtUtc;
    }

    private static void EnsureNotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                "invalid_identifier",
                $"{parameterName} is required.");
        }
    }

    private static void EnsureName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleViolationException("organization_name_required", "Organization name is required.");
        }

        if (name.Length is < NameMinLength or > NameMaxLength)
        {
            throw new DomainRuleViolationException("organization_name_length", "Organization name length is invalid.");
        }
    }

    private static void EnsureNormalizedName(string normalizedName)
    {
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new DomainRuleViolationException("organization_normalized_name_required", "Normalized organization name is required.");
        }
    }

    private static void EnsureDescription(string? description)
    {
        if (description?.Length > DescriptionMaxLength)
        {
            throw new DomainRuleViolationException("organization_description_length", "Organization description length is invalid.");
        }
    }

    private void EnsureActive(string errorCode, string message)
    {
        if (Status == OrganizationStatus.Archived)
        {
            throw new DomainRuleViolationException(errorCode, message);
        }
    }
}
