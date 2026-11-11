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

    /// <summary>
    /// Stops every play link issued so far: adds a new policy that links are signed against from now on, then deletes
    /// the old one. Blob storage may take up to 30 seconds to notice. One rotation at a time, or two could each keep
    /// their own policy.
    /// </summary>
    public async Task RevokeAllAsync()
    {
        var container = blobs.GetBlobContainerClient(Track.AudioContainer);
        await _changingPolicy.WaitAsync();
        try
        {
            var replacement = NewPolicy();
            var old = (await container.GetAccessPolicyAsync()).Value.SignedIdentifiers;
            await container.SetAccessPolicyAsync(permissions: [.. old, replacement]);
            await container.SetAccessPolicyAsync(permissions: [replacement]);
        }
        finally
        {
            _changingPolicy.Release();
        }
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

            var created = NewPolicy();
            await container.SetAccessPolicyAsync(permissions: [created]);
            return created.Id;
        }
        finally
        {
            _changingPolicy.Release();
        }
    }

    private static BlobSignedIdentifier NewPolicy() => new()
    {
        Id = Guid.CreateVersion7().ToString("N"),
        AccessPolicy = new BlobAccessPolicy { Permissions = "r" },
    };

    // The newest: mid-rotation the new policy sits after the old one, which is about to go.
    private static async Task<string?> ReadPolicyAsync(BlobContainerClient container)
    {
        var policy = await container.GetAccessPolicyAsync();
        return policy.Value.SignedIdentifiers.LastOrDefault()?.Id;
    }
}
