using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class LoginTests(KleinGeldApp app)
{
    [Fact]
    public async Task Anonymous_calls_to_Member_endpoints_are_refused()
    {
        using var client = app.CreateWebClient();

        using var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_Admin_logs_in_and_calls_Member_endpoints()
    {
        using var client = await app.LoginAsAdminAsync();

        var me = await client.GetFromJsonAsync<MeResponse>("/api/me");

        // Not ManagesArtist: tests share the app, and some give the Admin an Artist of their own.
        Assert.Equal(KleinGeldApp.AdminEmail, me!.Email);
        Assert.True(me.IsAdmin);
    }

    [Fact]
    public async Task A_wrong_password_doesnt_log_in()
    {
        using var client = await app.LoginAsync(KleinGeldApp.AdminEmail, "not-the-password");

        using var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logging_out_ends_the_session()
    {
        using var client = await app.LoginAsAdminAsync();
        var form = await IdentityForms.ReadAsync(client, "/Account/Manage");
        form["ReturnUrl"] = "";

        using var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(form));
        logout.EnsureSuccessStatusCode();
        using var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Every_page_with_a_Log_out_button_clears_the_saved_player_state_on_logout()
    {
        using var client = await app.LoginAsNewMemberAsync();

        var page = await client.GetStringAsync("/Account/Manage");

        Assert.Contains("logout", page[page.IndexOf("<script", StringComparison.Ordinal)..]);
        var script = await client.GetStringAsync("/logout.js");
        Assert.Contains("localStorage.removeItem(\"kleingeld.player\")", script);
    }

    [Fact]
    public async Task A_changed_security_stamp_refuses_the_next_request()
    {
        // Identity re-checks the stamp only once time has passed since the cookie was issued, which a clock that
        // another test froze never does on its own.
        var loggedInAt = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        await app.SetClockAsync(loggedInAt);
        using var client = await app.LoginAsAdminAsync();

        await app.ChangeSecurityStampAsync(KleinGeldApp.AdminEmail);
        await app.SetClockAsync(loggedInAt.AddSeconds(1));
        using var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task There_is_no_Register_page()
    {
        using var client = app.CreateWebClient();

        using var response = await client.GetAsync("/Account/Register");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
