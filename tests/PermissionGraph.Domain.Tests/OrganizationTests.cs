using FluentAssertions;
using PermissionGraph.Domain.Common;
using PermissionGraph.Domain.Organizations;

namespace PermissionGraph.Domain.Tests;

public sealed class OrganizationTests
{
    private static readonly Guid OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_InitializesActiveOrganization()
    {
        var organization = CreateOrganization();

        organization.Id.Should().Be(OrganizationId);
        organization.Name.Should().Be("Acme Engineering");
        organization.NormalizedName.Should().Be("ACME ENGINEERING");
        organization.OwnerUserId.Should().Be(OwnerUserId);
        organization.Status.Should().Be(OrganizationStatus.Active);
        organization.PolicyVersion.Should().Be(1);
        organization.IsActive.Should().BeTrue();
        organization.CreatedAtUtc.Should().Be(Now);
        organization.UpdatedAtUtc.Should().Be(Now);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "22222222-2222-2222-2222-222222222222")]
    [InlineData("11111111-1111-1111-1111-111111111111", "00000000-0000-0000-0000-000000000000")]
    public void Create_RejectsRequiredIdentifiers(string organizationId, string ownerUserId)
    {
        var act = () => Organization.Create(
            Guid.Parse(organizationId),
            "Acme Engineering",
            "ACME ENGINEERING",
            null,
            Guid.Parse(ownerUserId),
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
        var act = () => Organization.Create(OrganizationId, name, "ACME ENGINEERING", null, OwnerUserId, Now);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Create_RejectsEmptyNormalizedName()
    {
        var act = () => Organization.Create(OrganizationId, "Acme Engineering", "", null, OwnerUserId, Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "organization_normalized_name_required");
    }

    [Fact]
    public void UpdateDetails_ChangesEditableDetails_WhenActive()
    {
        var organization = CreateOrganization();
        var updatedAt = Now.AddMinutes(5);

        organization.UpdateDetails("Acme Platform", "ACME PLATFORM", "New description", updatedAt);

        organization.Name.Should().Be("Acme Platform");
        organization.NormalizedName.Should().Be("ACME PLATFORM");
        organization.Description.Should().Be("New description");
        organization.UpdatedAtUtc.Should().Be(updatedAt);
    }

    [Fact]
    public void UpdateDetails_RejectsArchivedOrganization()
    {
        var organization = CreateOrganization();
        organization.Archive(Now.AddMinutes(1));

        var act = () => organization.UpdateDetails("Acme Platform", "ACME PLATFORM", null, Now.AddMinutes(2));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "archived_organization_cannot_be_updated");
    }

    [Fact]
    public void Archive_MarksOrganizationArchived()
    {
        var organization = CreateOrganization();
        var archivedAt = Now.AddMinutes(5);

        organization.Archive(archivedAt);

        organization.Status.Should().Be(OrganizationStatus.Archived);
        organization.IsActive.Should().BeFalse();
        organization.UpdatedAtUtc.Should().Be(archivedAt);
    }

    [Fact]
    public void Archive_RejectsSecondArchive()
    {
        var organization = CreateOrganization();
        organization.Archive(Now.AddMinutes(1));

        var act = () => organization.Archive(Now.AddMinutes(2));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "organization_already_archived");
    }

    [Fact]
    public void TransferOwnership_ChangesOwnerAndIncrementsPolicyVersion()
    {
        var organization = CreateOrganization();
        var newOwnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var transferredAt = Now.AddMinutes(5);

        organization.TransferOwnership(newOwnerId, transferredAt);

        organization.OwnerUserId.Should().Be(newOwnerId);
        organization.PolicyVersion.Should().Be(2);
        organization.UpdatedAtUtc.Should().Be(transferredAt);
    }

    [Fact]
    public void TransferOwnership_RejectsCurrentOwner()
    {
        var organization = CreateOrganization();

        var act = () => organization.TransferOwnership(OwnerUserId, Now.AddMinutes(5));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "ownership_transfer_to_current_owner");
    }

    [Fact]
    public void TransferOwnership_RejectsArchivedOrganization()
    {
        var organization = CreateOrganization();
        organization.Archive(Now.AddMinutes(1));

        var act = () => organization.TransferOwnership(Guid.NewGuid(), Now.AddMinutes(5));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "archived_organization_cannot_transfer_ownership");
    }

    private static Organization CreateOrganization()
    {
        return Organization.Create(
            OrganizationId,
            "Acme Engineering",
            "ACME ENGINEERING",
            null,
            OwnerUserId,
            Now);
    }
}
