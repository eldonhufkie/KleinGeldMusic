using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;
using KleinGeld.Web.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace KleinGeld.Web.Api;

internal static class AdminRemovalEndpoints
{
    /// <summary>
    /// The Admin pulling content: Rejecting a Published Release, or deleting any Release or Artist, always with a reason
    /// for its managing Members. The Admin never does a Takedown.
    /// </summary>
    public static void MapAdminRemovalEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/releases/{id:guid}/reject", async (
            Guid id,
            RejectForm form,
            KleinGeldDbContext db,
            CoverStore covers,
            BlobLinks links,
            PlayLinks playLinks,
            UserManager<Member> members,
            EmailSender emails,
            ILoggerFactory loggers) =>
        {
            Release? rejected = null;
            IReadOnlyList<string> unusedBlobs = [];
            var refused = await db.WithReviewedReleaseLockedAsync(id, async release =>
            {
                if (release.Stage != ReleaseStage.Published)
                {
                    return Results.Problem("Only a Published Release can be Rejected this way.", statusCode: StatusCodes.Status409Conflict);
                }

                // Any Pending Changes fold into the Release, as on a Takedown, which is then reviewed whole on resubmission.
                unusedBlobs = release.MergePendingChanges();
                release.Stage = ReleaseStage.Rejected;
                release.RejectionReason = form.Reason.Trim();
                await db.SaveChangesAsync();
                rejected = release;
                return null;
            });
            if (refused is not null)
            {
                return refused;
            }

            // After the commit, so a link fetched meanwhile is either refused or signed against the policy that goes.
            await playLinks.RevokeAllAsync();
            await DeleteBlobsAsync([], unusedBlobs, covers, links);
            var addresses = await ManagerAddressesAsync(rejected!.ArtistId, db, members);
            await SendAsync(addresses, a => MemberEmails.ReleaseRejected(a, rejected), rejected.Id, emails, loggers);
            return Results.Ok(rejected.ToDetails(covers));
        });

        api.MapPost("/releases/{id:guid}/delete", async (
            Guid id,
            DeleteForm form,
            KleinGeldDbContext db,
            CoverStore covers,
            BlobLinks links,
            PlayLinks playLinks,
            UserManager<Member> members,
            EmailSender emails,
            ILoggerFactory loggers) =>
        {
            List<string> blobNames = [];
            List<MailboxAddress> addresses = [];
            var what = "";
            var refused = await db.WithReviewedReleaseLockedAsync(id, async release =>
            {
                addresses = await ManagerAddressesAsync(release.ArtistId, db, members);
                what = $"\"{release.Title}\" by {release.Artist.Name}";
                blobNames = release.Tracks.SelectMany(t => t.BlobNames()).ToList();
                db.Releases.Remove(release);
                await db.SaveChangesAsync();
                return null;
            });
            if (refused is not null)
            {
                return refused;
            }

            await playLinks.RevokeAllAsync();
            await DeleteBlobsAsync([id], blobNames, covers, links);
            await SendAsync(addresses, a => MemberEmails.DeletedByAdmin(a, what, form.Reason.Trim()), id, emails, loggers);
            return Results.NoContent();
        });

        api.MapPost("/artists/{id:guid}/delete", async (
            Guid id,
            DeleteForm form,
            KleinGeldDbContext db,
            CoverStore covers,
            BlobLinks links,
            PlayLinks playLinks,
            UserManager<Member> members,
            EmailSender emails,
            ILoggerFactory loggers) =>
        {
            var artist = await db.Artists
                .Include(a => a.Releases).ThenInclude(r => r.Tracks)
                .SingleOrDefaultAsync(a => a.Id == id);
            if (artist is null)
            {
                return Results.NotFound();
            }

            var addresses = await ManagerAddressesAsync(id, db, members);
            var releaseIds = artist.Releases.Select(r => r.Id).ToList();
            var blobNames = artist.Releases.SelectMany(r => r.Tracks).SelectMany(t => t.BlobNames()).ToList();
            db.Artists.Remove(artist);
            await db.SaveChangesAsync();

            await playLinks.RevokeAllAsync();
            await DeleteBlobsAsync(releaseIds, blobNames, covers, links);
            await SendAsync(addresses, a => MemberEmails.DeletedByAdmin(a, $"the Artist {artist.Name}", form.Reason.Trim()), id, emails, loggers);
            return Results.NoContent();
        });
    }

    private static async Task DeleteBlobsAsync(IEnumerable<Guid> releaseIds, IEnumerable<string> blobNames, CoverStore covers, BlobLinks links)
    {
        foreach (var releaseId in releaseIds)
        {
            await covers.DeleteAllAsync(releaseId);
        }

        foreach (var blobName in blobNames)
        {
            await links.Blob(TrackEndpoints.AudioContainer, blobName).DeleteIfExistsAsync();
        }
    }

    /// <summary>The Artist's managing Members, except any Admin, who made the decision.</summary>
    private static async Task<List<MailboxAddress>> ManagerAddressesAsync(Guid artistId, KleinGeldDbContext db, UserManager<Member> members)
    {
        var adminIds = (await members.GetUsersInRoleAsync(Roles.Admin)).Select(a => a.Id).ToList();
        var addresses = await db.Artists
            .Where(a => a.Id == artistId)
            .SelectMany(a => a.Managers)
            .Where(m => !adminIds.Contains(m.Id))
            .Select(m => m.Email!)
            .ToListAsync();
        return addresses.Select(a => new MailboxAddress(null, a)).ToList();
    }

    /// <summary>The change stands whatever happens here, so a failure is only logged.</summary>
    private static async Task SendAsync(
        IEnumerable<MailboxAddress> addresses, Func<MailboxAddress, OutgoingEmail> email, Guid id, EmailSender emails, ILoggerFactory loggers)
    {
        try
        {
            foreach (var address in addresses)
            {
                await emails.SendAsync(email(address));
            }
        }
        catch (Exception exception)
        {
            loggers.CreateLogger(typeof(AdminRemovalEndpoints)).LogError(exception, "Couldn't email the managers about {Id}.", id);
        }
    }
}
