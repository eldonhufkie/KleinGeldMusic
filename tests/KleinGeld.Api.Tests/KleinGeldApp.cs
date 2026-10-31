using System.Net.Http.Json;

namespace KleinGeld.Api.Tests;

/// <summary>
/// Starts the whole distributed app once (PostgreSQL, Azurite, MailPit, migrator, web) for every API test.
/// </summary>
public sealed class KleinGeldApp : IAsyncLifetime
{
    public const string AdminEmail = "admin@kleingeld.test";
    public const string AdminPassword = "Admin-Pa55word!";

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

    private DistributedApplication? _app;

    private DistributedApplication App => _app ?? throw new InvalidOperationException("The app hasn't started.");

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.KleinGeld_AppHost>(
        [
            $"Parameters:admin-email={AdminEmail}",
            $"Parameters:admin-password={AdminPassword}",
            "Postgres:DataVolume=false",
        ]);
        builder.CreateResourceBuilder<ProjectResource>("web")
            .WithEnvironment("Testing__Enabled", "true");

        _app = await builder.BuildAsync().WaitAsync(StartupTimeout);
        await _app.StartAsync().WaitAsync(StartupTimeout);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("web").WaitAsync(StartupTimeout);
    }

    /// <summary>A client with its own cookies, like a separate browser.</summary>
    public HttpClient CreateWebClient() =>
        new(new HttpClientHandler { CookieContainer = new() }) { BaseAddress = App.GetEndpoint("web", "https") };

    /// <summary>Logs in through the real Login page and returns a client holding whatever cookie that gave.</summary>
    public async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = CreateWebClient();
        var form = await IdentityForms.ReadAsync(client, "/Account/Login");
        form["Input.Email"] = email;
        form["Input.Password"] = password;

        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();
        return client;
    }

    public Task<HttpClient> LoginAsAdminAsync() => LoginAsync(AdminEmail, AdminPassword);

    /// <summary>Sets the time the web host's <see cref="TimeProvider"/> reports from now on.</summary>
    public async Task SetClockAsync(DateTimeOffset now)
    {
        using var client = CreateWebClient();
        using var response = await client.PutAsJsonAsync("/testing/clock", now);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Changes a Member's security stamp, as a password change or a Remove would.</summary>
    public async Task ChangeSecurityStampAsync(string email)
    {
        using var client = CreateWebClient();
        using var response = await client.PostAsJsonAsync("/testing/security-stamp", email);
        response.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class KleinGeldAppCollection : ICollectionFixture<KleinGeldApp>
{
    public const string Name = "KleinGeld app";
}
