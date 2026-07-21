namespace PermissionGraph.Infrastructure.Seeding.AuthorizationSeed;

public sealed class PermissionDefinitionRecord
{
    public Guid Id { get; set; }

    public Guid? OrganizationId { get; set; }

    public required string Key { get; set; }

    public required string NormalizedKey { get; set; }

    public required string DisplayName { get; set; }

    public string? Description { get; set; }

    public required string Module { get; set; }

    public required string PermissionType { get; set; }

    public required string AllowedScopes { get; set; }

    public bool IsRequestable { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}