namespace PermissionGraph.Application.Abstractions.Services.Users;

public interface ICurrentUser
{
    Guid? UserId { get; }
}