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

        Assert.Equal(new MeResponse(KleinGeldApp.AdminEmail, IsAdmin: true), me);
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
    public async Task A_changed_security_stamp_refuses_the_next_request()
    {
        using var client = await app.LoginAsAdminAsync();

        await app.ChangeSecurityStampAsync(KleinGeldApp.AdminEmail);
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
