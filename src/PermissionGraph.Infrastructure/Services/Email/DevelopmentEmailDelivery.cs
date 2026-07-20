using PermissionGraph.Application.Abstractions.Email;

namespace PermissionGraph.Infrastructure.Email;

internal sealed class DevelopmentEmailDelivery : IEmailDelivery
{
    public Task SendEmailConfirmationAsync(Guid userId, string email, string token, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(Guid userId, string email, string token, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
