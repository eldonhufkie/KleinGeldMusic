using System.Net.Http.Json;
using System.Text.RegularExpressions;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class PasswordLinkTests(KleinGeldApp app)
{
    [Fact]
    public async Task An_invalid_link_points_to_Forgot_password_and_the_Admin()
    {
        using var browser = app.CreateWebClient();

        var page = await browser.GetStringAsync($"/Account/SetUpPassword?memberId={Guid.NewGuid()}&code=bm9wZQ");

        Assert.Contains("This link has expired", page);
        Assert.Contains("Account/ForgotPassword", page);
        Assert.Contains("ask the Admin to send the email again", page);
    }

    [Fact]
    public async Task Forgot_password_sends_email_3_whose_link_resets_the_password()
    {
        var email = await CreateMemberAsync();
        await SetPasswordAsync(await ReadLinkAsync(email, "Welcome to KleinGeld Music"), "First-Pa55word!");

        await ForgotPasswordAsync(email);
        await SetPasswordAsync(await ReadLinkAsync(email, "Reset your KleinGeld Music password"), "Second-Pa55word!");

        using var member = await app.LoginAsync(email, "Second-Pa55word!");
        Assert.Equal(new MeResponse(email, IsAdmin: false), await member.GetFromJsonAsync<MeResponse>("/api/me"));
    }

    [Fact]
    public async Task Forgot_password_works_for_a_Member_who_never_set_one()
    {
        var email = await CreateMemberAsync();

        await ForgotPasswordAsync(email);
        await SetPasswordAsync(await ReadLinkAsync(email, "Reset your KleinGeld Music password"), "First-Pa55word!");

        using var member = await app.LoginAsync(email, "First-Pa55word!");
        Assert.Equal(new MeResponse(email, IsAdmin: false), await member.GetFromJsonAsync<MeResponse>("/api/me"));
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_email_sends_nothing_and_looks_the_same()
    {
        var email = UniqueEmail();

        await ForgotPasswordAsync(email);

        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.Empty(await app.CreateMailPit().SearchAsync(email, "KleinGeld Music"));
    }

    [Fact]
    public async Task The_reset_link_is_refused_after_15_minutes()
    {
        var requestedAt = new DateTimeOffset(2033, 1, 2, 12, 0, 0, TimeSpan.Zero);
        var email = await CreateMemberAsync();
        await app.SetClockAsync(requestedAt);
        await ForgotPasswordAsync(email);
        var link = await ReadLinkAsync(email, "Reset your KleinGeld Music password");

        await app.SetClockAsync(requestedAt.AddMinutes(16));
        using var browser = app.CreateWebClient();
        var page = await browser.GetStringAsync(link.PathAndQuery);

        Assert.Contains("This link has expired", page);
    }

    [Fact]
    public async Task The_Admin_resends_email_1_to_a_Member_without_a_password_and_never_sees_the_link()
    {
        var email = await CreateMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var waiting = await FindWaitingAsync(admin, email);

        using var response = await admin.PostAsync($"/api/admin/members/{waiting.Id}/resend-setup-email", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        var emails = await app.CreateMailPit().WaitForEmailsAsync(email, "Welcome to KleinGeld Music", count: 2);
        var resent = new Uri(Regex.Match(emails[0].Text, @"https://\S+").Value);
        await SetPasswordAsync(resent, "First-Pa55word!");
        using var member = await app.LoginAsync(email, "First-Pa55word!");
        Assert.Equal(new MeResponse(email, IsAdmin: false), await member.GetFromJsonAsync<MeResponse>("/api/me"));
    }

    [Fact]
    public async Task A_Member_who_has_set_a_password_is_no_longer_waiting_and_cant_be_resent_email_1()
    {
        var email = await CreateMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var waiting = await FindWaitingAsync(admin, email);
        await SetPasswordAsync(await ReadLinkAsync(email, "Welcome to KleinGeld Music"), "First-Pa55word!");

        var list = await admin.GetFromJsonAsync<List<MemberAwaitingPassword>>("/api/admin/members/awaiting-password");
        using var response = await admin.PostAsync($"/api/admin/members/{waiting.Id}/resend-setup-email", null);

        Assert.DoesNotContain(list!, m => m.Email == email);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Only_the_Admin_can_resend_email_1()
    {
        using var anonymous = app.CreateWebClient();

        using var response = await anonymous.PostAsync($"/api/admin/members/{Guid.NewGuid()}/resend-setup-email", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>An approved Member with no password yet, through the real Join Request flow.</summary>
    private async Task<string> CreateMemberAsync()
    {
        var (name, email) = ($"Outsider {Guid.NewGuid():N}", UniqueEmail());
        using (var client = app.CreateWebClient())
        {
            using var submitted = await client.PostAsJsonAsync("/api/join-requests", new JoinRequestForm
            {
                Name = name,
                Email = email,
                Message = "Hello, I'd like to join.",
            });
            Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        }

        using var admin = await app.LoginAsAdminAsync();
        var open = await admin.GetFromJsonAsync<List<OpenJoinRequest>>("/api/admin/join-requests");
        var joinRequest = Assert.Single(open!, r => r.Name == name);
        using var approved = await admin.PostAsync($"/api/admin/join-requests/{joinRequest.Id}/approve", null);
        Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);
        return email;
    }

    private static async Task<MemberAwaitingPassword> FindWaitingAsync(HttpClient admin, string email)
    {
        var waiting = await admin.GetFromJsonAsync<List<MemberAwaitingPassword>>("/api/admin/members/awaiting-password");
        return Assert.Single(waiting!, m => m.Email == email);
    }

    private async Task ForgotPasswordAsync(string email)
    {
        using var browser = app.CreateWebClient();
        var form = await IdentityForms.ReadAsync(browser, "/Account/ForgotPassword");
        form["Input.Email"] = email;

        using var response = await browser.PostAsync("/Account/ForgotPassword", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();
        Assert.Contains("Check your email", await response.Content.ReadAsStringAsync());
    }

    private async Task<Uri> ReadLinkAsync(string email, string subject)
    {
        var sent = await app.CreateMailPit().WaitForEmailAsync(email, subject);
        Assert.Equal(subject, sent.Subject);
        return new Uri(Regex.Match(sent.Text, @"https://\S+").Value);
    }

    /// <summary>Fills in the page a password link opens, in a fresh browser, and checks it was accepted.</summary>
    private async Task SetPasswordAsync(Uri link, string password)
    {
        using var browser = app.CreateWebClient();
        var form = await IdentityForms.ReadAsync(browser, link.PathAndQuery);
        form["Input.Password"] = form["Input.ConfirmPassword"] = password;

        using var response = await browser.PostAsync(link.PathAndQuery, new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();
        Assert.DoesNotContain("This link has expired", await response.Content.ReadAsStringAsync());
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.test";
}
