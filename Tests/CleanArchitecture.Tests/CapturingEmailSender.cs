using Microsoft.AspNetCore.Identity;

namespace CleanArchitecture.Tests;

/// <summary>Merkt sich die Bestätigungslinks, statt Mails zu versenden. Der Link ist wie bei <c>MapIdentityApi</c> HTML-kodiert.</summary>
public sealed class CapturingEmailSender : IEmailSender<IdentityUser>
{
    public List<(string Email, string Link)> Confirmations { get; } = [];

    public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink)
    {
        lock (Confirmations)
        {
            Confirmations.Add((email, confirmationLink));
        }

        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink) => Task.CompletedTask;

    public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode) => Task.CompletedTask;
}
