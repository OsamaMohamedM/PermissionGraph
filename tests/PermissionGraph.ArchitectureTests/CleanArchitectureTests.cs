namespace PermissionGraph.ArchitectureTests;

public sealed class CleanArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(PermissionGraph.Domain.Common.AssemblyMarker).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IClock).Assembly;
    private static readonly Assembly ContractsAssembly = typeof(PermissionGraph.Contracts.AssemblyMarker).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(PermissionGraphDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(AuthEndpoints).Assembly;
    private static readonly Assembly WorkerAssembly = typeof(PermissionGraph.Worker.AssemblyMarker).Assembly;

    [Fact]
    public void Domain_HasNoProjectReferences()
    {
        DomainAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .Should()
            .NotContain(name => name.StartsWith("PermissionGraph.", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_ReferencesOnlyDomainProject()
    {
        ProjectReferences("src/PermissionGraph.Application/PermissionGraph.Application.csproj")
            .Should()
            .BeEquivalentTo(["PermissionGraph.Domain"]);
    }

    [Fact]
    public void Contracts_HasNoProjectReferences()
    {
        ContractsAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .Should()
            .NotContain(name => name.StartsWith("PermissionGraph.", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceApi()
    {
        ProjectReferences("src/PermissionGraph.Infrastructure/PermissionGraph.Infrastructure.csproj")
            .Should()
            .NotContain("PermissionGraph.Api");
    }

    [Fact]
    public void Api_ReferencesOnlyApprovedProjects()
    {
        ProjectReferences("src/PermissionGraph.Api/PermissionGraph.Api.csproj")
            .Should()
            .BeEquivalentTo([
                "PermissionGraph.Application",
                "PermissionGraph.Contracts",
                "PermissionGraph.Infrastructure"
            ]);
    }

    [Fact]
    public void Worker_ReferencesOnlyInfrastructureProject()
    {
        ProjectReferences("src/PermissionGraph.Worker/PermissionGraph.Worker.csproj")
            .Should()
            .BeEquivalentTo(["PermissionGraph.Infrastructure"]);
    }

    [Fact]
    public void Application_DoesNotReferenceFrameworkInfrastructurePackages()
    {
        var forbidden = new[]
        {
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "StackExchange.Redis",
            "Serilog"
        };

        ApplicationAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Should()
            .NotContain(name => forbidden.Any(name.StartsWith));
    }

    [Fact]
    public void NoGenericRepositoryAbstractionExists()
    {
        Types.InAssemblies([
                DomainAssembly,
                ApplicationAssembly,
                InfrastructureAssembly
            ])
            .That()
            .HaveNameMatching(".*GenericRepository.*|.*Repository`.*")
            .GetTypes()
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void Infrastructure_DoesNotUseIdentityRoles()
    {
        Types.InAssembly(InfrastructureAssembly)
            .That()
            .HaveDependencyOn("Microsoft.AspNetCore.Identity.IdentityRole")
            .GetTypes()
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void LaterMilestoneAuthorizationEngineTypesDoNotExistInM04()
    {
        Types.InAssemblies([
                DomainAssembly,
                ApplicationAssembly,
                InfrastructureAssembly,
                ApiAssembly
            ])
            .That()
            .HaveNameMatching(".*(RoleAssignment|AuthorizationEngine).*")
            .GetTypes()
            .Should()
            .BeEmpty();
    }

    private static string[] ProjectReferences(string projectPath)
    {
        var projectFile = Path.Combine(FindRepositoryRoot(), projectPath.Replace('/', Path.DirectorySeparatorChar));
        var document = XDocument.Load(projectFile);

        return document.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .OfType<string>()
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermissionGraph.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}