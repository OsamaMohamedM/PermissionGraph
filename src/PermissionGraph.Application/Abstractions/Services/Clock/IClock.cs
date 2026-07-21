namespace PermissionGraph.Application.Abstractions.Services.Clock;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}