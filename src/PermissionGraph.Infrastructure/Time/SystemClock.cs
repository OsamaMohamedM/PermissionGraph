using PermissionGraph.Application.Abstractions.Clock;

namespace PermissionGraph.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
