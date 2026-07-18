namespace PermissionGraph.Application.Abstractions.Email;

public interface IEmailDelivery
{
    Task SendEmailConfirmationAsync(Guid userId, string email, string token, CancellationToken cancellationToken);

    Task SendPasswordResetAsync(Guid userId, string email, string token, CancellationToken cancellationToken);
}
