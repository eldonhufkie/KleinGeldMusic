using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using KleinGeld.Data;

namespace KleinGeld.Web.Components.Account;

internal sealed class IdentityNoOpEmailSender : IEmailSender<Member>
{
    private readonly IEmailSender emailSender = new NoOpEmailSender();

    public Task SendConfirmationLinkAsync(Member user, string email, string confirmationLink) =>
        emailSender.SendEmailAsync(email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

    public Task SendPasswordResetLinkAsync(Member user, string email, string resetLink) =>
        emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

    public Task SendPasswordResetCodeAsync(Member user, string email, string resetCode) =>
        emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password using the following code: {resetCode}");
}
