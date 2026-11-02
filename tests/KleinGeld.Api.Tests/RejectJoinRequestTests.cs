using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class RejectJoinRequestTests(KleinGeldApp app)
{
    [Fact]
    public async Task A_rejected_outsider_gets_a_short_polite_email_with_no_reason()
    {
        var (name, email) = (UniqueName(), UniqueEmail());
        await SubmitAsync(name, email);
        using var admin = await app.LoginAsAdminAsync();
        var joinRequest = await FindOpenAsync(admin, name);

        using var response = await admin.PostAsync($"/api/admin/join-requests/{joinRequest.Id}/reject", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var rejected = await app.CreateMailPit().WaitForEmailAsync(email, "KleinGeld Music");
        Assert.Equal("Your Join Request to KleinGeld Music", rejected.Subject);
        Assert.Equal(
            $"""
            Hi {name},

            Thank you for asking to join KleinGeld Music. Unfortunately the Admin couldn't accept your Join Request.

            Kind regards,
            KleinGeld Music
            """.ReplaceLineEndings(),
            rejected.Text.TrimEnd().ReplaceLineEndings());
        var open = await admin.GetFromJsonAsync<List<OpenJoinRequest>>("/api/admin/join-requests");
        Assert.DoesNotContain(open!, r => r.Id == joinRequest.Id);
    }

    [Fact]
    public async Task A_Join_Request_from_a_Members_email_is_flagged_and_rejectable()
    {
        var name = UniqueName();
        await SubmitAsync(name, KleinGeldApp.AdminEmail.ToUpperInvariant());
        using var admin = await app.LoginAsAdminAsync();

        var joinRequest = await FindOpenAsync(admin, name);
        using var response = await admin.PostAsync($"/api/admin/join-requests/{joinRequest.Id}/reject", null);

        Assert.True(joinRequest.EmailBelongsToMember);
        Assert.False(joinRequest.DuplicateOfOpenRequest);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await app.CreateMailPit().WaitForEmailAsync(KleinGeldApp.AdminEmail, $"Hi {name},");
    }

    [Fact]
    public async Task A_Join_Request_duplicating_an_open_one_is_flagged_and_rejectable()
    {
        var (first, second) = (UniqueName(), UniqueName());
        var email = UniqueEmail();
        await SubmitAsync(first, email);
        await SubmitAsync(second, email.ToUpperInvariant());
        using var admin = await app.LoginAsAdminAsync();

        var original = await FindOpenAsync(admin, first);
        var duplicate = await FindOpenAsync(admin, second);
        using var response = await admin.PostAsync($"/api/admin/join-requests/{duplicate.Id}/reject", null);

        Assert.False(original.DuplicateOfOpenRequest);
        Assert.True(duplicate.DuplicateOfOpenRequest);
        Assert.False(duplicate.EmailBelongsToMember);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await app.CreateMailPit().WaitForEmailAsync(email, $"Hi {second},");
        Assert.Equal(original, await FindOpenAsync(admin, first));
    }

    [Fact]
    public async Task Rejecting_twice_sends_one_email()
    {
        var (name, email) = (UniqueName(), UniqueEmail());
        await SubmitAsync(name, email);
        using var admin = await app.LoginAsAdminAsync();
        var joinRequest = await FindOpenAsync(admin, name);

        using var first = await admin.PostAsync($"/api/admin/join-requests/{joinRequest.Id}/reject", null);
        using var second = await admin.PostAsync($"/api/admin/join-requests/{joinRequest.Id}/reject", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        await app.CreateMailPit().WaitForEmailAsync(email, name);
        Assert.Single(await app.CreateMailPit().SearchAsync(email, name));
    }

    [Fact]
    public async Task Only_the_Admin_rejects_Join_Requests()
    {
        var name = UniqueName();
        await SubmitAsync(name, UniqueEmail());
        using var admin = await app.LoginAsAdminAsync();
        var joinRequest = await FindOpenAsync(admin, name);
        using var anonymous = app.CreateWebClient();

        using var response = await anonymous.PostAsync($"/api/admin/join-requests/{joinRequest.Id}/reject", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(joinRequest, await FindOpenAsync(admin, name));
    }

    private static async Task<OpenJoinRequest> FindOpenAsync(HttpClient admin, string name)
    {
        var open = await admin.GetFromJsonAsync<List<OpenJoinRequest>>("/api/admin/join-requests");
        return Assert.Single(open!, r => r.Name == name);
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
