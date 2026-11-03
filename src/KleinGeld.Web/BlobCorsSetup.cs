using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace KleinGeld.Web;

/// <summary>
/// Lets browsers on the site's origin (<c>Blobs:CorsOrigin</c>) play and upload audio straight from blob storage.
/// Set at startup for Azurite (Test, development); Production's Azure account is configured by hand.
/// </summary>
internal sealed class BlobCorsSetup(BlobServiceClient blobs, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var origin = configuration["Blobs:CorsOrigin"];
        if (string.IsNullOrEmpty(origin))
        {
            return;
        }

        var properties = (await blobs.GetPropertiesAsync(cancellationToken)).Value;
        properties.Cors = [new BlobCorsRule
        {
            AllowedOrigins = origin,
            AllowedMethods = "GET,HEAD,PUT,OPTIONS",
            AllowedHeaders = "*",
            ExposedHeaders = "*",
            MaxAgeInSeconds = 3600,
        }];
        await blobs.SetPropertiesAsync(properties, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
