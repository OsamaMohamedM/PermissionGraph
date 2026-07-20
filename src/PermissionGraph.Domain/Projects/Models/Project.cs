using PermissionGraph.Domain.Common;

namespace PermissionGraph.Domain.Projects;

public sealed class Project
{
    public const int NameMinLength = 3;
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 2000;

    private Project(
        Guid id,
        Guid organizationId,
        string name,
        string normalizedName,
        string? description,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
        Status = ProjectStatus.Active;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    private Project()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public string? Description { get; private set; }

    public ProjectStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public bool IsActive => Status == ProjectStatus.Active;

    public static Project Create(
        Guid id,
        Guid organizationId,
        string name,
        string normalizedName,
        string? description,
        DateTimeOffset createdAtUtc)
    {
        EnsureNotEmpty(id, nameof(id));
        EnsureNotEmpty(organizationId, nameof(organizationId));
        EnsureName(name);
        EnsureNormalizedName(normalizedName);
        EnsureDescription(description);

        return new Project(id, organizationId, name, normalizedName, description, createdAtUtc);
    }

    public void UpdateDetails(
        string name,
        string normalizedName,
        string? description,
        DateTimeOffset updatedAtUtc)
    {
        EnsureActive("archived_project_cannot_be_updated", "Archived project cannot be updated.");
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
        EnsureActive("project_already_archived", "Archived project cannot be archived again.");

        Status = ProjectStatus.Archived;
        ArchivedAtUtc = archivedAtUtc;
        UpdatedAtUtc = archivedAtUtc;
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
            throw new DomainRuleViolationException("project_name_required", "Project name is required.");
        }

        if (name.Length is < NameMinLength or > NameMaxLength)
        {
            throw new DomainRuleViolationException("project_name_length", "Project name length is invalid.");
        }
    }

    private static void EnsureNormalizedName(string normalizedName)
    {
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new DomainRuleViolationException("project_normalized_name_required", "Normalized project name is required.");
        }
    }

    private static void EnsureDescription(string? description)
    {
        if (description?.Length > DescriptionMaxLength)
        {
            throw new DomainRuleViolationException("project_description_length", "Project description length is invalid.");
        }
    }

    private void EnsureActive(string errorCode, string message)
    {
        if (Status == ProjectStatus.Archived)
        {
            throw new DomainRuleViolationException(errorCode, message);
        }
    }
}
