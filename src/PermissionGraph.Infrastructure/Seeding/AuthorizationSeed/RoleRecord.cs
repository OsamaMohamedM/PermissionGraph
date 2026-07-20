namespace PermissionGraph.Infrastructure.AuthorizationSeed;

public sealed class RoleRecord
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public required string Name { get; set; }

    public required string NormalizedName { get; set; }

    public string? Description { get; set; }

    public required string ScopeType { get; set; }

    public required string RoleType { get; set; }

    public bool IsRequestable { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
