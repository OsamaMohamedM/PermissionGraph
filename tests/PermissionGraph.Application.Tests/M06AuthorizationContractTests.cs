namespace PermissionGraph.Application.Tests;

public sealed class M06AuthorizationContractTests
{
    private static readonly Guid SubjectUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrganizationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ProjectId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset HistoricalTime = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CheckPermissionQuery_NormalizesPermissionKeyAndBuildsScope()
    {
        var query = new CheckPermissionQuery(
            SubjectUserId,
            OrganizationId,
            ProjectId,
            "pg.projects.view");

        query.NormalizedPermissionKey.Should().Be("pg.projects.view");
        query.Scope.OrganizationId.Should().Be(OrganizationId);
        query.Scope.ProjectId.Should().Be(ProjectId);
    }

    [Fact]
    public void CheckPermissionQueryValidator_AllowsOmittedSubjectForCurrentUserDefault()
    {
        var validator = new CheckPermissionQueryValidator();
        var query = new CheckPermissionQuery(null, OrganizationId, null, "pg.organizations.view");

        var result = validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CheckPermissionQueryValidator_RejectsEmptySubjectWhenProvided()
    {
        var validator = new CheckPermissionQueryValidator();
        var query = new CheckPermissionQuery(Guid.Empty, OrganizationId, null, "pg.organizations.view");

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CheckPermissionQuery.SubjectUserId));
    }

    [Fact]
    public void CheckPermissionQueryValidator_RejectsEmptyOrganization()
    {
        var validator = new CheckPermissionQueryValidator();
        var query = new CheckPermissionQuery(SubjectUserId, Guid.Empty, null, "pg.organizations.view");

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CheckPermissionQuery.OrganizationId));
    }

    [Fact]
    public void CheckPermissionQueryValidator_RejectsInvalidPermissionKey()
    {
        var validator = new CheckPermissionQueryValidator();
        var query = new CheckPermissionQuery(SubjectUserId, OrganizationId, null, "");

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CheckPermissionQuery.PermissionKey));
    }

    [Fact]
    public void CheckPermissionQueryValidator_RejectsHistoricalTime()
    {
        var validator = new CheckPermissionQueryValidator();
        var query = new CheckPermissionQuery(
            SubjectUserId,
            OrganizationId,
            null,
            "pg.organizations.view",
            HistoricalTime);

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CheckPermissionQuery.RequestedEvaluationTimeUtc));
    }

    [Fact]
    public void BatchCheckPermissionsQueryValidator_RejectsEmptyBatch()
    {
        var validator = new BatchCheckPermissionsQueryValidator();
        var query = new BatchCheckPermissionsQuery([]);

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(BatchCheckPermissionsQuery.Checks));
    }

    [Fact]
    public void BatchCheckPermissionsQuery_PreservesDeterministicInputOrdering()
    {
        var query = new BatchCheckPermissionsQuery(
        [
            new BatchCheckPermissionItem("second", SubjectUserId, OrganizationId, null, "pg.roles.view"),
            new BatchCheckPermissionItem("first", SubjectUserId, OrganizationId, ProjectId, "pg.projects.view")
        ]);

        query.OrderedChecks.Select(check => check.CorrelationId)
            .Should()
            .Equal(["second", "first"]);
    }

    [Fact]
    public void BatchCheckPermissionsQueryValidator_EnforcesMaximumBatchSize()
    {
        var validator = new BatchCheckPermissionsQueryValidator();
        var checks = Enumerable.Range(0, BatchCheckPermissionsQuery.MaxChecks + 1)
            .Select(index => new BatchCheckPermissionItem(
                index.ToString(),
                SubjectUserId,
                OrganizationId,
                null,
                "pg.organizations.view"))
            .ToArray();

        var result = validator.Validate(new BatchCheckPermissionsQuery(checks));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(BatchCheckPermissionsQuery.Checks));
    }
}
