using System.Net.Http.Json;
using System.Text.RegularExpressions;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class ChangeEmailTests(KleinGeldApp app)
{
    private const string Password = "First-Pa55word!";
    private const string Subject = "Confirm your new KleinGeld Music email";

    [Fact]
    public async Task Email_4_goes_to_the_new_address_and_the_login_email_changes_only_once_its_link_is_opened()
    {
        var startedAt = new DateTimeOffset(2035, 3, 4, 12, 0, 0, TimeSpan.Zero);
        await app.SetClockAsync(startedAt);
        var (oldEmail, newEmail) = (await CreateMemberAsync(), UniqueEmail());
        using var member = await app.LoginAsync(oldEmail, Password);

        await RequestChangeAsync(member, newEmail);
        var link = await ReadLinkAsync(newEmail);

        Assert.Equal(new MeResponse(oldEmail, IsAdmin: false), await member.GetFromJsonAsync<MeResponse>("/api/me"));
        await AssertCanLogInAsync(oldEmail);

        var page = await member.GetStringAsync(link.PathAndQuery);
        await app.SetClockAsync(startedAt.AddSeconds(1));

        Assert.Contains("Your login email is now", page);
        Assert.Equal(new MeResponse(newEmail, IsAdmin: false), await member.GetFromJsonAsync<MeResponse>("/api/me"));
        await AssertCanLogInAsync(newEmail);
        await AssertCantLogInAsync(oldEmail);
    }

    [Fact]
    public async Task The_link_is_refused_after_15_minutes()
    {
        var requestedAt = new DateTimeOffset(2035, 4, 5, 12, 0, 0, TimeSpan.Zero);
        await app.SetClockAsync(requestedAt);
        var (oldEmail, newEmail) = (await CreateMemberAsync(), UniqueEmail());
        using var member = await app.LoginAsync(oldEmail, Password);
        await RequestChangeAsync(member, newEmail);
        var link = await ReadLinkAsync(newEmail);

        await app.SetClockAsync(requestedAt.AddMinutes(16));
        using var browser = app.CreateWebClient();
        var page = await browser.GetStringAsync(link.PathAndQuery);

        Assert.Contains("This link has expired", page);
        await AssertCanLogInAsync(oldEmail);
        await AssertCantLogInAsync(newEmail);
    }

    [Fact]
    public async Task The_link_works_once()
    {
        var (oldEmail, newEmail) = (await CreateMemberAsync(), UniqueEmail());
        using var member = await app.LoginAsync(oldEmail, Password);
        await RequestChangeAsync(member, newEmail);
        var link = await ReadLinkAsync(newEmail);
        using var browser = app.CreateWebClient();

        await browser.GetStringAsync(link.PathAndQuery);
        var page = await browser.GetStringAsync(link.PathAndQuery);

        Assert.Contains("This link has expired", page);
    }

    [Fact]
    public async Task A_link_edited_to_another_address_is_refused()
    {
        var (oldEmail, newEmail, otherEmail) = (await CreateMemberAsync(), UniqueEmail(), UniqueEmail());
        using var member = await app.LoginAsync(oldEmail, Password);
        await RequestChangeAsync(member, newEmail);
        var link = await ReadLinkAsync(newEmail);

        using var browser = app.CreateWebClient();
        var page = await browser.GetStringAsync(link.PathAndQuery.Replace(newEmail, otherEmail));

        Assert.Contains("This link has expired", page);
        await AssertCanLogInAsync(oldEmail);
    }

    [Fact]
    public async Task A_link_with_a_broken_code_or_unknown_Member_gets_the_expired_link_page()
    {
        using var browser = app.CreateWebClient();

        var brokenCode = await browser.GetStringAsync($"/Account/ConfirmEmailChange?userId={Guid.NewGuid()}&email=a%40example.test&code=%21%21%21");
        var unknownMember = await browser.GetStringAsync($"/Account/ConfirmEmailChange?userId={Guid.NewGuid()}&email=a%40example.test&code=bm9wZQ");

        Assert.Contains("This link has expired", brokenCode);
        Assert.Contains("This link has expired", unknownMember);
    }

    [Fact]
    public async Task Asking_for_another_Members_email_looks_the_same_and_sends_nothing()
    {
        var (email, taken) = (await CreateMemberAsync(), await CreateMemberAsync());
        using var member = await app.LoginAsync(email, Password);

        var page = await RequestChangeAsync(member, taken);

        Assert.Contains("We sent a link to the new address", page);
        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.Empty(await app.CreateMailPit().SearchAsync(taken, Subject));
    }

    [Fact]
    public async Task An_address_taken_after_the_link_was_sent_is_refused_and_nothing_changes()
    {
        var (first, second, wanted) = (await CreateMemberAsync(), await CreateMemberAsync(), UniqueEmail());
        using var firstMember = await app.LoginAsync(first, Password);
        using var secondMember = await app.LoginAsync(second, Password);
        await RequestChangeAsync(firstMember, wanted);
        await RequestChangeAsync(secondMember, wanted);
        var links = await app.CreateMailPit().WaitForEmailsAsync(wanted, Subject, count: 2);
        var (secondLink, firstLink) = (ReadLink(links[0]), ReadLink(links[1]));

        using var browser = app.CreateWebClient();
        await browser.GetStringAsync(firstLink.PathAndQuery);
        var page = await browser.GetStringAsync(secondLink.PathAndQuery);

        Assert.Contains("This link has expired", page);
        await AssertCanLogInAsync(wanted);
        await AssertCanLogInAsync(second);
    }

    [Fact]
    public async Task An_address_Identity_wouldnt_take_as_a_user_name_still_works_for_login()
    {
        var (oldEmail, newEmail) = (await CreateMemberAsync(), $"o'{UniqueEmail()}");
        using var member = await app.LoginAsync(oldEmail, Password);
        await RequestChangeAsync(member, newEmail);
        var link = await ReadLinkAsync(newEmail);

        using var browser = app.CreateWebClient();
        await browser.GetStringAsync(link.PathAndQuery);

        await AssertCanLogInAsync(newEmail);
        await AssertCantLogInAsync(oldEmail);
    }

    private async Task AssertCanLogInAsync(string email)
    {
        using var client = await app.LoginAsync(email, Password);
        Assert.Equal(email, (await client.GetFromJsonAsync<MeResponse>("/api/me"))!.Email);
    }

    private async Task AssertCantLogInAsync(string email)
    {
        using var client = await app.LoginAsync(email, Password);
        using var response = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Fills in the Manage email page and returns the page it answers with.</summary>
    private static async Task<string> RequestChangeAsync(HttpClient member, string newEmail)
    {
        var form = await IdentityForms.ReadAsync(member, "/Account/Manage/Email");
        form["Input.NewEmail"] = newEmail;

        using var response = await member.PostAsync("/Account/Manage/Email", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<Uri> ReadLinkAsync(string email)
    {
        var sent = await app.CreateMailPit().WaitForEmailAsync(email, Subject);
        Assert.Equal(Subject, sent.Subject);
        return ReadLink(sent);
    }

    private static Uri ReadLink(MailPit.Email email) => new(Regex.Match(email.Text, @"https://\S+").Value);

    /// <summary>An approved Member with a password, through the real Join Request flow.</summary>
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

        var sent = await app.CreateMailPit().WaitForEmailAsync(email, "Welcome to KleinGeld Music");
        var link = new Uri(Regex.Match(sent.Text, @"https://\S+").Value);
        using var browser = app.CreateWebClient();
        var form = await IdentityForms.ReadAsync(browser, link.PathAndQuery);
        form["Input.Password"] = form["Input.ConfirmPassword"] = Password;
        using var response = await browser.PostAsync(link.PathAndQuery, new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();
        return email;
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.test";
}
