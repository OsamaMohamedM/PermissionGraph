namespace PermissionGraph.Application.Abstractions.Users;

public interface ICurrentUser
{
    Guid? UserId { get; }
}
