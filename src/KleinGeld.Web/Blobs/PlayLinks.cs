using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using KleinGeld.Contracts;
using KleinGeld.Data;

namespace KleinGeld.Web.Blobs;

/// <summary>
/// Issues play links, signed against the stored access policy on the audio container: replacing that policy is what
/// revokes every outstanding link at once. The policy only grants reading; each link carries its own expiry.
/// </summary>
internal sealed class PlayLinks(BlobServiceClient blobs, BlobLinks links, TimeProvider clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    // Setting a policy replaces all others, so two callers must never both find none and create one.
    private readonly SemaphoreSlim _changingPolicy = new(1, 1);

    public async Task<PlayLink> ForAsync(string blobName)
    {
        var policy = await CurrentPolicyAsync();
        var expiresOn = clock.GetUtcNow() + Lifetime;
        return new PlayLink(links.ForReading(Track.AudioContainer, blobName, policy, expiresOn), expiresOn);
    }

    private async Task<string> CurrentPolicyAsync()
    {
        var container = blobs.GetBlobContainerClient(Track.AudioContainer);
        if (await ReadPolicyAsync(container) is { } policy)
        {
            return policy;
        }

        await _changingPolicy.WaitAsync();
        try
        {
            if (await ReadPolicyAsync(container) is { } createdMeanwhile)
            {
                return createdMeanwhile;
            }

            var created = new BlobSignedIdentifier
            {
                Id = Guid.CreateVersion7().ToString("N"),
                AccessPolicy = new BlobAccessPolicy { Permissions = "r" },
            };
            await container.SetAccessPolicyAsync(permissions: [created]);
            return created.Id;
        }
        finally
        {
            _changingPolicy.Release();
        }
    }

    private static async Task<string?> ReadPolicyAsync(BlobContainerClient container)
    {
        var policy = await container.GetAccessPolicyAsync();
        return policy.Value.SignedIdentifiers.FirstOrDefault()?.Id;
    }
}
