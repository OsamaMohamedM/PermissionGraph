namespace PermissionGraph.Infrastructure.Projects;

public sealed class ProjectAdministratorAssignmentRecord
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid CreatedByUserId { get; set; }
}
