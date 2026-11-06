using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using KleinGeld.Contracts;

namespace KleinGeld.Web.Blobs;

/// <summary>
/// Short-lived SAS links that let browsers reach blob storage directly, for any kind of file. Containers are created
/// on first use and are always private: a link is the only way in.
/// </summary>
internal sealed class BlobLinks(BlobServiceClient blobs, TimeProvider clock, IConfiguration configuration)
{
    private static readonly TimeSpan UploadLinkLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// A link that can only create <paramref name="blobName"/>, never replace it: blob names are write-once.
    /// Whoever issues it picks a new name for every upload and keeps <paramref name="uploadId"/> to find it again.
    /// </summary>
    public async Task<UploadLink> ForUploadAsync(string container, string blobName, Guid uploadId)
    {
        var client = blobs.GetBlobContainerClient(container);
        await client.CreateIfNotExistsAsync(PublicAccessType.None);

        var expiresOn = clock.GetUtcNow() + UploadLinkLifetime;
        var url = client.GetBlobClient(blobName).GenerateSasUri(BlobSasPermissions.Create, expiresOn);
        return new UploadLink(uploadId, AsSeenByBrowsers(url), expiresOn);
    }

    public Uri ForReading(string container, string blobName, TimeSpan lifetime)
    {
        var blob = blobs.GetBlobContainerClient(container).GetBlobClient(blobName);
        return AsSeenByBrowsers(blob.GenerateSasUri(BlobSasPermissions.Read, clock.GetUtcNow() + lifetime));
    }

    /// <summary>A link whose permissions come from the stored access policy <paramref name="policy"/>, so it dies with it.</summary>
    public Uri ForReading(string container, string blobName, string policy, DateTimeOffset expiresOn)
    {
        var blob = blobs.GetBlobContainerClient(container).GetBlobClient(blobName);
        return AsSeenByBrowsers(blob.GenerateSasUri(new BlobSasBuilder
        {
            BlobContainerName = container,
            BlobName = blobName,
            Resource = "b",
            Identifier = policy,
            ExpiresOn = expiresOn,
        }));
    }

    public BlobClient Blob(string container, string blobName) =>
        blobs.GetBlobContainerClient(container).GetBlobClient(blobName);

    /// <summary>
    /// A link for this server's own tools, such as ffprobe, so it keeps the address this host uses. Never hand it to
    /// a browser. Blob storage checks expiry against its own clock, which the app's clock may not match in tests.
    /// </summary>
    public Uri ForServerReading(string container, string blobName) =>
        Blob(container, blobName).GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddMinutes(5));

    /// <summary>The Test copy's Azurite is reached at another address by browsers than by this host.</summary>
    private Uri AsSeenByBrowsers(Uri url)
    {
        var publicEndpoint = configuration["Blobs:PublicEndpoint"];
        if (string.IsNullOrEmpty(publicEndpoint))
        {
            return url;
        }

        var endpoint = new Uri(publicEndpoint);
        return new UriBuilder(url) { Scheme = endpoint.Scheme, Host = endpoint.Host, Port = endpoint.Port }.Uri;
    }
}
