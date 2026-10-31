using System.Net.Http.Json;

namespace KleinGeld.Api.Tests;

/// <summary>
/// Starts the whole distributed app once (PostgreSQL, Azurite, MailPit, migrator, web) for every API test.
/// </summary>
public sealed class KleinGeldApp : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

    private DistributedApplication? _app;

    private DistributedApplication App => _app ?? throw new InvalidOperationException("The app hasn't started.");

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.KleinGeld_AppHost>();
        builder.CreateResourceBuilder<ProjectResource>("web")
            .WithEnvironment("Testing__ControllableClock", "true");

        _app = await builder.BuildAsync().WaitAsync(StartupTimeout);
        await _app.StartAsync().WaitAsync(StartupTimeout);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("web").WaitAsync(StartupTimeout);
    }

    public HttpClient CreateWebClient() => App.CreateHttpClient("web", "https");

    /// <summary>Sets the time the web host's <see cref="TimeProvider"/> reports from now on.</summary>
    public async Task SetClockAsync(DateTimeOffset now)
    {
        using var client = CreateWebClient();
        using var response = await client.PutAsJsonAsync("/testing/clock", now);
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
