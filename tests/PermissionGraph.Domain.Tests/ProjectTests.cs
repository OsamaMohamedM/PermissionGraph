namespace PermissionGraph.Domain.Tests;

public sealed class ProjectTests
{
    private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrganizationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_InitializesActiveProject()
    {
        var project = CreateProject();

        project.Id.Should().Be(ProjectId);
        project.OrganizationId.Should().Be(OrganizationId);
        project.Name.Should().Be("Launch Control");
        project.NormalizedName.Should().Be("LAUNCH CONTROL");
        project.Description.Should().Be("Coordinate the platform launch.");
        project.Status.Should().Be(ProjectStatus.Active);
        project.IsActive.Should().BeTrue();
        project.CreatedAtUtc.Should().Be(Now);
        project.UpdatedAtUtc.Should().Be(Now);
        project.ArchivedAtUtc.Should().BeNull();
        project.Version.Should().Be(0);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "22222222-2222-2222-2222-222222222222")]
    [InlineData("11111111-1111-1111-1111-111111111111", "00000000-0000-0000-0000-000000000000")]
    public void Create_RejectsRequiredIdentifiers(string projectId, string organizationId)
    {
        var act = () => Project.Create(
            Guid.Parse(projectId),
            Guid.Parse(organizationId),
            "Launch Control",
            "LAUNCH CONTROL",
            null,
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "invalid_identifier");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public void Create_RejectsInvalidName(string name)
    {
        var act = () => Project.Create(ProjectId, OrganizationId, name, "LAUNCH CONTROL", null, Now);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Create_RejectsNameLongerThanMaximum()
    {
        var act = () => Project.Create(
            ProjectId,
            OrganizationId,
            new string('a', Project.NameMaxLength + 1),
            "LAUNCH CONTROL",
            null,
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "project_name_length");
    }

    [Fact]
    public void Create_RejectsEmptyNormalizedName()
    {
        var act = () => Project.Create(ProjectId, OrganizationId, "Launch Control", "", null, Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "project_normalized_name_required");
    }

    [Fact]
    public void Create_RejectsDescriptionLongerThanMaximum()
    {
        var act = () => Project.Create(
            ProjectId,
            OrganizationId,
            "Launch Control",
            "LAUNCH CONTROL",
            new string('a', Project.DescriptionMaxLength + 1),
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "project_description_length");
    }

    [Fact]
    public void UpdateDetails_ChangesEditableDetails_WhenActive()
    {
        var project = CreateProject();
        var updatedAt = Now.AddMinutes(5);

        project.UpdateDetails("Billing Portal", "BILLING PORTAL", "New description", updatedAt);

        project.Name.Should().Be("Billing Portal");
        project.NormalizedName.Should().Be("BILLING PORTAL");
        project.Description.Should().Be("New description");
        project.UpdatedAtUtc.Should().Be(updatedAt);
        project.Version.Should().Be(0);
    }

    [Fact]
    public void UpdateDetails_RejectsInvalidDetails()
    {
        var project = CreateProject();

        var act = () => project.UpdateDetails("ab", "BILLING PORTAL", null, Now.AddMinutes(5));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "project_name_length");
    }

    [Fact]
    public void UpdateDetails_RejectsArchivedProject()
    {
        var project = CreateProject();
        project.Archive(Now.AddMinutes(1));

        var act = () => project.UpdateDetails("Billing Portal", "BILLING PORTAL", null, Now.AddMinutes(2));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "archived_project_cannot_be_updated");
    }

    [Fact]
    public void Archive_MarksProjectArchived()
    {
        var project = CreateProject();
        var archivedAt = Now.AddMinutes(5);

        project.Archive(archivedAt);

        project.Status.Should().Be(ProjectStatus.Archived);
        project.IsActive.Should().BeFalse();
        project.ArchivedAtUtc.Should().Be(archivedAt);
        project.UpdatedAtUtc.Should().Be(archivedAt);
        project.Version.Should().Be(0);
    }

    [Fact]
    public void Archive_RejectsSecondArchive()
    {
        var project = CreateProject();
        project.Archive(Now.AddMinutes(1));

        var act = () => project.Archive(Now.AddMinutes(2));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "project_already_archived");
    }

    private static Project CreateProject()
    {
        return Project.Create(
            ProjectId,
            OrganizationId,
            "Launch Control",
            "LAUNCH CONTROL",
            "Coordinate the platform launch.",
            Now);
    }
}