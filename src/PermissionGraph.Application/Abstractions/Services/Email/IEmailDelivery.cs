namespace PermissionGraph.Application.Abstractions.Services.Email;

public interface IEmailDelivery
{
    Task SendEmailConfirmationAsync(Guid userId, string email, string token, CancellationToken cancellationToken);

    Task SendPasswordResetAsync(Guid userId, string email, string token, CancellationToken cancellationToken);
}