using System.Data.Common;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace KleinGeld.Web.Email;

/// <summary>An email KleinGeld Music sends, in plain text.</summary>
internal sealed record OutgoingEmail(MailboxAddress To, string Subject, string Text);

/// <summary>
/// Sends emails over SMTP to whatever the <c>smtp</c> connection string points at: MailPit in development and
/// tests, Gmail in Production. Changing provider is a configuration change.
/// </summary>
internal sealed class EmailSender(SmtpSettings settings)
{
    public async Task SendAsync(OutgoingEmail email)
    {
        var message = new MimeMessage();
        message.From.Add(settings.From);
        message.To.Add(email.To);
        message.Subject = email.Subject;
        message.Body = new TextPart("plain") { Text = email.Text };

        // A hung server mustn't keep the person who caused the email waiting for MailKit's default 2 minutes.
        using var smtp = new SmtpClient { Timeout = (int)TimeSpan.FromSeconds(15).TotalMilliseconds };
        // Auto: implicit TLS on port 465, STARTTLS when the server offers it (Gmail on 587), plain for MailPit.
        await smtp.ConnectAsync(settings.Endpoint.Host, settings.Endpoint.Port, SecureSocketOptions.Auto);
        if (settings.Username is not null)
        {
            await smtp.AuthenticateAsync(settings.Username, settings.Password ?? "");
        }
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(quit: true);
    }
}

/// <summary>
/// Where emails go, from the <c>smtp</c> connection string (<c>Endpoint=smtp://host:port;Username=…;Password=…</c>),
/// and who they come from, from <c>Email:From</c>.
/// </summary>
internal sealed record SmtpSettings(Uri Endpoint, string? Username, string? Password, MailboxAddress From)
{
    public static SmtpSettings FromConfiguration(IConfiguration configuration)
    {
        var connection = new DbConnectionStringBuilder
        {
            ConnectionString = configuration.GetConnectionString("smtp")
                ?? throw new InvalidOperationException("The smtp connection string is missing."),
        };
        var from = configuration["Email:From"] ?? throw new InvalidOperationException("Email:From is missing.");

        return new(
            new Uri((string)connection["Endpoint"]),
            connection.TryGetValue("Username", out var username) ? (string)username : null,
            connection.TryGetValue("Password", out var password) ? (string)password : null,
            MailboxAddress.Parse(from));
    }
}

internal static class EmailSenderExtensions
{
    public static void AddEmailSender(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(SmtpSettings.FromConfiguration(builder.Configuration));
        builder.Services.AddSingleton<EmailSender>();
    }
}
