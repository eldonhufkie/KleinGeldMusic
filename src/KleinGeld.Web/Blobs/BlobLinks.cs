using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace KleinGeld.Web.Blobs;

/// <summary>
/// Short-lived SAS links to private blobs, signed with the account key. Expiry follows the real time, not the app's
/// <see cref="TimeProvider"/>, because blob storage checks it against its own clock.
/// </summary>
internal sealed class BlobLinks(BlobServiceClient blobs, IConfiguration configuration)
{
    private static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ServerReadLifetime = TimeSpan.FromMinutes(5);

    public BlobClient Blob(string container, string blobName) =>
        blobs.GetBlobContainerClient(container).GetBlobClient(blobName);

    /// <summary>
    /// A link a browser can upload one file to, in a single PUT. It can only create the blob, never overwrite it,
    /// which keeps blob names write-once.
    /// </summary>
    public async Task<Uri> UploadLinkAsync(string container, string blobName)
    {
        await blobs.GetBlobContainerClient(container).CreateIfNotExistsAsync();
        var link = Blob(container, blobName).GenerateSasUri(BlobSasPermissions.Create, DateTimeOffset.UtcNow + UploadLifetime);
        return AsSeenByBrowsers(link);
    }

    /// <summary>A link for this server's own tools, such as ffprobe. Never hand it to a browser.</summary>
    public Uri ServerReadLink(string container, string blobName) =>
        Blob(container, blobName).GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow + ServerReadLifetime);

    /// <summary>The Test copy's Azurite has one address inside the compose network and another for browsers.</summary>
    private Uri AsSeenByBrowsers(Uri link)
    {
        var publicEndpoint = configuration["Blobs:PublicEndpoint"];
        if (string.IsNullOrEmpty(publicEndpoint))
        {
            return link;
        }

        var relative = link.PathAndQuery[blobs.Uri.AbsolutePath.TrimEnd('/').Length..];
        return new Uri(publicEndpoint.TrimEnd('/') + relative);
    }
}
