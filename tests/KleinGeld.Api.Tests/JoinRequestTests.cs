using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class JoinRequestTests(KleinGeldApp app)
{
    private const string MemberNote = "This email already belongs to a Member.";
    private const string DuplicateNote = "There is already an open Join Request from this email.";

    [Fact]
    public async Task Submitting_a_Join_Request_emails_the_Admin()
    {
        var name = UniqueName();

        var answer = await SubmitAsync(name, "outsider@example.test", "I play bass in De Kleine Kapel.");

        Assert.Equal(HttpStatusCode.Accepted, answer.Status);
        var email = await app.CreateMailPit().WaitForEmailAsync(KleinGeldApp.AdminEmail, name);
        Assert.Equal($"New Join Request from {name}", email.Subject);
        Assert.Contains("outsider@example.test", email.Text);
        Assert.Contains("I play bass in De Kleine Kapel.", email.Text);
    }

    [Fact]
    public async Task The_answer_doesnt_tell_whether_the_email_is_a_Member_or_already_asked()
    {
        var newEmail = UniqueEmail();

        var forNewEmail = await SubmitAsync(UniqueName(), newEmail);
        var forOpenRequestEmail = await SubmitAsync(UniqueName(), newEmail);
        var forMemberEmail = await SubmitAsync(UniqueName(), KleinGeldApp.AdminEmail);

        Assert.Equal(HttpStatusCode.Accepted, forNewEmail.Status);
        Assert.Equal(forNewEmail, forOpenRequestEmail);
        Assert.Equal(forNewEmail, forMemberEmail);
    }

    [Fact]
    public async Task The_Admin_is_told_when_the_email_already_belongs_to_a_Member()
    {
        var name = UniqueName();

        await SubmitAsync(name, KleinGeldApp.AdminEmail.ToUpperInvariant());

        var email = await app.CreateMailPit().WaitForEmailAsync(KleinGeldApp.AdminEmail, name);
        Assert.Contains(MemberNote, email.Text);
        Assert.DoesNotContain(DuplicateNote, email.Text);
    }

    [Fact]
    public async Task The_Admin_is_told_when_the_email_already_has_an_open_Join_Request()
    {
        var (first, second) = (UniqueName(), UniqueName());
        var outsider = UniqueEmail();

        await SubmitAsync(first, outsider);
        await SubmitAsync(second, outsider.ToUpperInvariant());

        var mailPit = app.CreateMailPit();
        var firstEmail = await mailPit.WaitForEmailAsync(KleinGeldApp.AdminEmail, first);
        var secondEmail = await mailPit.WaitForEmailAsync(KleinGeldApp.AdminEmail, second);
        Assert.DoesNotContain(DuplicateNote, firstEmail.Text);
        Assert.Contains(DuplicateNote, secondEmail.Text);
        Assert.DoesNotContain(MemberNote, secondEmail.Text);
    }

    [Theory]
    [InlineData("", "outsider@example.test", "Hello {name}")]
    [InlineData("{name}", "not-an-email", "Hello")]
    [InlineData("{name}", "outsider@example.test", "")]
    [InlineData("{name}", "outsider@example.test", "{long}")]
    public async Task An_incomplete_or_invalid_Join_Request_is_refused(string name, string email, string message)
    {
        var marker = UniqueName();

        var answer = await SubmitAsync(
            name.Replace("{name}", marker),
            email,
            message
                .Replace("{name}", marker)
                .Replace("{long}", marker + new string('x', JoinRequestForm.MessageMaxLength)));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Empty(await app.CreateMailPit().SearchAsync(KleinGeldApp.AdminEmail, marker));
    }

    /// <summary>Submits a Join Request as an outsider and returns everything the answer could reveal.</summary>
    private async Task<Answer> SubmitAsync(string name, string email, string message = "Hello, I'd like to join.")
    {
        using var client = app.CreateWebClient();
        using var response = await client.PostAsJsonAsync("/api/join-requests", new JoinRequestForm
        {
            Name = name,
            Email = email,
            Message = message,
        });
        return new(response.StatusCode, response.Content.Headers.ContentType?.ToString(), await response.Content.ReadAsStringAsync());
    }

    private sealed record Answer(HttpStatusCode Status, string? ContentType, string Body);

    /// <summary>A name no other test uses, so this test can find its own email in the shared MailPit.</summary>
    private static string UniqueName() => $"Outsider {Guid.NewGuid():N}";

    /// <summary>An email that belongs to no Member and no other test's Join Request.</summary>
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.test";
}
