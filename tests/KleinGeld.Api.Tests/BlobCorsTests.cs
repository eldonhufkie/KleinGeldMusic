namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class BlobCorsTests(KleinGeldApp app)
{
    // Browsers play and upload audio straight from blob storage, so it must accept requests from the site.
    [Fact]
    public async Task Blob_storage_accepts_browser_requests_from_the_site()
    {
        var blobs = await app.CreateBlobServiceClientAsync();

        var properties = (await blobs.GetPropertiesAsync()).Value;

        var rule = Assert.Single(properties.Cors);
        Assert.Equal(KleinGeldApp.SiteOrigin, rule.AllowedOrigins);
        Assert.Equal("GET,HEAD,PUT,OPTIONS", rule.AllowedMethods);
    }
}
