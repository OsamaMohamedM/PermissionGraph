using PermissionGraph.Application.Abstractions.Identifiers;

namespace PermissionGraph.Infrastructure.Identifiers;

internal sealed class GuidProvider : IGuidProvider
{
    public Guid NewGuid()
    {
        return Guid.NewGuid();
    }
}
