using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;
using KleinGeld.Web.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
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
            Guid id, RejectForm form, KleinGeldDbContext db, [AsParameters] RemovalStores stores, [AsParameters] ManagerMail mail) =>
        {
            Release? rejected = null;
            var removed = Removed.Nothing;
            var refused = await db.WithReviewedReleaseLockedAsync(id, async release =>
            {
                if (release.Stage != ReleaseStage.Published)
                {
                    return Results.Problem("Only a Published Release can be Rejected this way.", statusCode: StatusCodes.Status409Conflict);
                }

                // Any Pending Changes fold into the Release, as on a Takedown, which is then reviewed whole on resubmission.
                removed = new Removed([], release.MergePendingChanges(), AnyPublished: true);
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

            // Only unused audio goes: the Release keeps its covers.
            await stores.CleanUpAsync(removed);
            var addresses = await ManagingMemberAddressesAsync(rejected!.ArtistId, db, mail.Members);
            await mail.SendAsync(addresses, a => MemberEmails.ReleaseRejected(a, rejected), rejected.Id);
            return Results.Ok(rejected.ToDetails(stores.Covers));
        });

        api.MapPost("/releases/{id:guid}/delete", async (
            Guid id, DeleteForm form, KleinGeldDbContext db, [AsParameters] RemovalStores stores, [AsParameters] ManagerMail mail) =>
        {
            var removed = Removed.Nothing;
            List<MailboxAddress> addresses = [];
            var deletedName = "";
            var refused = await db.WithReviewedReleaseLockedAsync(id, async release =>
            {
                addresses = await ManagingMemberAddressesAsync(release.ArtistId, db, mail.Members);
                deletedName = $"\"{release.Title}\" by {release.Artist.Name}";
                removed = await db.RemoveReleaseAsync(release);
                return null;
            });
            if (refused is not null)
            {
                return refused;
            }

            await stores.CleanUpAsync(removed);
            await mail.SendAsync(addresses, a => MemberEmails.DeletedByAdmin(a, deletedName, form.Reason.Trim()), id);
            return Results.NoContent();
        });

        api.MapPost("/artists/{id:guid}/delete", async (
            Guid id, DeleteForm form, KleinGeldDbContext db, [AsParameters] RemovalStores stores, [AsParameters] ManagerMail mail) =>
        {
            var removed = Removed.Nothing;
            List<MailboxAddress> addresses = [];
            var deletedName = "";
            var result = await db.WithReviewedArtistLockedAsync(id, async artist =>
            {
                addresses = await ManagingMemberAddressesAsync(id, db, mail.Members);
                deletedName = $"the Artist {artist.Name}";
                removed = await db.RemoveArtistAsync(artist);
                return Results.NoContent();
            });
            if (result is not NoContent)
            {
                return result;
            }

            await stores.CleanUpAsync(removed);
            await mail.SendAsync(addresses, a => MemberEmails.DeletedByAdmin(a, deletedName, form.Reason.Trim()), id);
            return result;
        });
    }
    /// <summary>The Artist's managing Members, except any Admin, who made the decision.</summary>
    private static async Task<List<MailboxAddress>> ManagingMemberAddressesAsync(Guid artistId, KleinGeldDbContext db, UserManager<Member> members)
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

    /// <summary>What telling an Artist's managing Members about the Admin's decision takes.</summary>
    internal sealed record ManagerMail(UserManager<Member> Members, EmailSender Emails, ILoggerFactory Loggers)
    {
        /// <summary>The change stands whatever happens here, so a failure is only logged.</summary>
        public async Task SendAsync(IEnumerable<MailboxAddress> addresses, Func<MailboxAddress, OutgoingEmail> email, Guid id)
        {
            try
            {
                foreach (var address in addresses)
                {
                    await Emails.SendAsync(email(address));
                }
            }
            catch (Exception exception)
            {
                Loggers.CreateLogger(typeof(AdminRemovalEndpoints)).LogError(exception, "Couldn't email the managing Members about {Id}.", id);
            }
        }
    }
}