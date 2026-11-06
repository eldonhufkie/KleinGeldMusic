using System.Net.Http.Json;
using System.Text.RegularExpressions;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

/// <summary>Brings new Members in the way real ones arrive: Join Request, approval, set-password link, login.</summary>
internal static class NewMembers
{
    private const string Password = "New-Member-Pa55word!";

    /// <summary>A client logged in as a Member nobody else has met, with the name they asked to join under.</summary>
    public static async Task<HttpClient> LoginAsNewMemberAsync(this KleinGeldApp app, string? name = null, string? email = null)
    {
        name ??= $"Member {Guid.NewGuid():N}";
        email ??= $"{Guid.NewGuid():N}@example.test";

        using var outsider = app.CreateWebClient();
        using var submitted = await outsider.PostAsJsonAsync("/api/join-requests", new JoinRequestForm
        {
            Name = name,
            Email = email,
            Message = "Hello, I'd like to join.",
        });
        submitted.EnsureSuccessStatusCode();

        using var admin = await app.LoginAsAdminAsync();
        var open = await admin.GetFromJsonAsync<List<OpenJoinRequest>>("/api/admin/join-requests");
        var joinRequest = Assert.Single(open!, r => r.Email == email);
        using var approved = await admin.PostAsync($"/api/admin/join-requests/{joinRequest.Id}/approve", null);
        approved.EnsureSuccessStatusCode();

        var welcome = await app.CreateMailPit().WaitForEmailAsync(email, "KleinGeld Music");
        var link = new Uri(Regex.Match(welcome.Text, @"https://\S+").Value);
        using var browser = app.CreateWebClient();
        var form = await IdentityForms.ReadAsync(browser, link.PathAndQuery);
        form["Input.Password"] = form["Input.ConfirmPassword"] = Password;
        using var passwordSet = await browser.PostAsync(link.PathAndQuery, new FormUrlEncodedContent(form));
        passwordSet.EnsureSuccessStatusCode();

        return await app.LoginAsync(email, Password);
    }
}
