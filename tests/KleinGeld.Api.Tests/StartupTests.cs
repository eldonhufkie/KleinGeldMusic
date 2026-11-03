namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class StartupTests(KleinGeldApp app)
{
    [Fact]
    public async Task Web_is_healthy_once_the_database_is_migrated()
    {
        using var client = app.CreateWebClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
