namespace PermissionGraph.Infrastructure.Services.Identifiers;

internal sealed class GuidProvider : IGuidProvider
{
    public Guid NewGuid()
    {
        return Guid.NewGuid();
    }
}