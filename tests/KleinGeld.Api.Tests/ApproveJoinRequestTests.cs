using System.Net.Http.Json;
using System.Text.RegularExpressions;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class ApproveJoinRequestTests(KleinGeldApp app)
{
    [Fact]
    public async Task The_Admin_sees_open_Join_Requests_with_name_email_message_and_time()
    {
        var instant = new DateTimeOffset(2031, 5, 6, 7, 8, 9, TimeSpan.Zero);
        await app.SetClockAsync(instant);
        var (name, email) = (UniqueName(), UniqueEmail());
        await SubmitAsync(name, email, "I play the tuba.");

        using var admin = await app.LoginAsAdminAsync();
        var open = await admin.GetFromJsonAsync<List<OpenJoinRequest>>("/api/admin/join-requests");

        var joinRequest = Assert.Single(open!, r => r.Name == name);
        Assert.Equal(email, joinRequest.Email);
        Assert.Equal("I play the tuba.", joinRequest.Message);
        Assert.Equal(instant, joinRequest.CreatedAt);
    }

    [Fact]
    public async Task Only_the_Admin_sees_Join_Requests()
    {
        using var anonymous = app.CreateWebClient();

        using var response = await anonymous.GetAsync("/api/admin/join-requests");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_approved_outsider_sets_a_password_from_the_email_link_and_logs_in()
    {
        var (name, email) = (UniqueName(), UniqueEmail());
        await SubmitAsync(name, email);
        using var admin = await app.LoginAsAdminAsync();

        await ApproveAsync(admin, name);
        var link = await ReadSetPasswordLinkAsync(email);
        await SetPasswordAsync(link, "New-Member-Pa55word!");
        using var member = await app.LoginAsync(email, "New-Member-Pa55word!");

        Assert.Equal(new MeResponse(email, IsAdmin: false), await member.GetFromJsonAsync<MeResponse>("/api/me"));
        var open = await admin.GetFromJsonAsync<List<OpenJoinRequest>>("/api/admin/join-requests");
        Assert.DoesNotContain(open!, r => r.Name == name);
    }

    [Fact]
    public async Task The_set_password_link_is_refused_after_15_minutes()
    {
        var approvedAt = new DateTimeOffset(2032, 3, 4, 12, 0, 0, TimeSpan.Zero);
        await app.SetClockAsync(approvedAt);
        var (name, email) = (UniqueName(), UniqueEmail());
        await SubmitAsync(name, email);
        using var admin = await app.LoginAsAdminAsync();
        await ApproveAsync(admin, name);
        var link = await ReadSetPasswordLinkAsync(email);
        using var browser = app.CreateWebClient();
        var form = await IdentityForms.ReadAsync(browser, link.PathAndQuery);

        await app.SetClockAsync(approvedAt.AddMinutes(16));
        var page = await browser.GetStringAsync(link.PathAndQuery);
        form["Input.Password"] = form["Input.ConfirmPassword"] = "New-Member-Pa55word!";
        using var submitted = await browser.PostAsync(link.PathAndQuery, new FormUrlEncodedContent(form));
        using var member = await app.LoginAsync(email, "New-Member-Pa55word!");

        Assert.Contains("This link has expired", page);
        Assert.Contains("This link has expired", await submitted.Content.ReadAsStringAsync());
        using var response = await member.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task ApproveAsync(HttpClient admin, string name)
    {
        var open = await admin.GetFromJsonAsync<List<OpenJoinRequest>>("/api/admin/join-requests");
        var joinRequest = Assert.Single(open!, r => r.Name == name);

        using var response = await admin.PostAsync($"/api/admin/join-requests/{joinRequest.Id}/approve", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>The set-password link from email 1, as the new Member would click it.</summary>
    private async Task<Uri> ReadSetPasswordLinkAsync(string email)
    {
        var approved = await app.CreateMailPit().WaitForEmailAsync(email, "KleinGeld Music");
        Assert.Equal("Welcome to KleinGeld Music", approved.Subject);
        return new Uri(Regex.Match(approved.Text, @"https://\S+").Value);
    }

    /// <summary>Fills in the page the link opens, in a fresh browser.</summary>
    private async Task<string> SetPasswordAsync(Uri link, string password)
    {
        using var browser = app.CreateWebClient();
        var form = await IdentityForms.ReadAsync(browser, link.PathAndQuery);
        form["Input.Password"] = password;
        form["Input.ConfirmPassword"] = password;

        using var response = await browser.PostAsync(link.PathAndQuery, new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task SubmitAsync(string name, string email, string message = "Hello, I'd like to join.")
    {
        using var client = app.CreateWebClient();
        using var response = await client.PostAsJsonAsync("/api/join-requests", new JoinRequestForm
        {
            Name = name,
            Email = email,
            Message = message,
        });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    /// <summary>A name no other test uses, so this test can find its own Join Request and emails.</summary>
    private static string UniqueName() => $"Outsider {Guid.NewGuid():N}";

    /// <summary>An email that belongs to no Member and no other test's Join Request.</summary>
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.test";
}
