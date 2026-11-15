using KleinGeld.Contracts;
using KleinGeld.Data;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web.Api;

/// <summary>What deleting a Member deleted: who it was, and the Artists that went with them.</summary>
internal sealed record DeletedMember(string Email, IReadOnlyList<DeletedArtist> Artists);

/// <summary>How <see cref="MemberDeletion.DeleteAsync"/> went.</summary>
internal enum MemberDeletionOutcome
{
    Deleted,
    NotFound,
    IsAdmin,
}

/// <summary>The outcome of deleting a Member, and what went when they were <see cref="MemberDeletionOutcome.Deleted"/>.</summary>
internal sealed record MemberDeletionResult(MemberDeletionOutcome Outcome, DeletedMember? Deleted = null);

/// <summary>
/// The one way a Member is deleted, when they Leave or the Admin Removes them: their Playlists, Identity rows,
/// passkeys and every Join Request with their email go; Artists only they manage go with all Releases and blobs; they stop
/// managing shared Artists; play links handed out are revoked. Sends no email: the caller decides that.
/// </summary>
internal static class MemberDeletion
{
    /// <summary>What <see cref="DeleteAsync"/> would delete, for the confirmation shown first.</summary>
    public static async Task<DeletionPreview> PreviewAsync(this KleinGeldDbContext db, Guid memberId)
    {
        var playlists = await db.Playlists
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.ChangedAt)
            .Select(p => p.Name)
            .ToListAsync();
        var artists = await db.Artists
            .ManagedBy(memberId)
            .Where(a => a.Managers.Count == 1)
            .OrderBy(a => a.Name)
            .Select(a => new DeletedArtist(a.Id, a.Name, a.Releases.OrderBy(r => r.Title).Select(r => r.Title).ToList()))
            .ToListAsync();
        return new DeletionPreview(playlists, artists, DeletionPreview.BackupWording);
    }

    /// <summary>Whether the Member is the Admin, who can't Leave or be Removed.</summary>
    public static Task<bool> IsAdminAsync(this KleinGeldDbContext db, Guid memberId) =>
        db.AdminIds().AnyAsync(id => id == memberId);

    /// <summary>The ids of the Members in the Admin role: the one Admin check, for single Members and lists alike.</summary>
    public static IQueryable<Guid> AdminIds(this KleinGeldDbContext db) =>
        db.UserRoles
            .Where(ur => db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Admin))
            .Select(ur => ur.UserId);

    /// <summary>
    /// Deletes the Member, holding the lock on every Artist they manage so no manager change races it, then cleans
    /// up blobs and revokes play links after the commit. A failed clean-up is logged, never thrown: the Member is
    /// gone by then, and the caller still has to answer and maybe email.
    /// </summary>
    public static async Task<MemberDeletionResult> DeleteAsync(
        this KleinGeldDbContext db, RemovalStores stores, ILogger logger, Guid memberId)
    {
        if (await db.IsAdminAsync(memberId))
        {
            return new MemberDeletionResult(MemberDeletionOutcome.IsAdmin);
        }

        var releaseIds = new List<Guid>();
        var blobNames = new List<string>();
        DeletedMember? deleted = null;
        // Retries run the whole transaction again, so nothing tracked by a failed attempt may carry over.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            releaseIds.Clear();
            blobNames.Clear();
            deleted = null;
            await using var transaction = await db.Database.BeginTransactionAsync();

            var member = await db.Users.SingleOrDefaultAsync(m => m.Id == memberId);
            if (member is null)
            {
                return;
            }

            // The same row lock as the Artist endpoints, taken in id order so two deletions can't deadlock.
            var artistIds = await db.Artists.ManagedBy(memberId).Select(a => a.Id).OrderBy(id => id).ToListAsync();
            foreach (var artistId in artistIds)
            {
                await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM "Artists" WHERE "Id" = {artistId} FOR UPDATE""");
            }

            var artists = await db.Artists.Include(a => a.Managers).Include(a => a.Releases)
                .Where(a => artistIds.Contains(a.Id)).OrderBy(a => a.Name).ToListAsync();
            var deletedArtists = new List<DeletedArtist>();
            foreach (var artist in artists)
            {
                if (artist.Managers.Count == 1)
                {
                    deletedArtists.Add(new DeletedArtist(artist.Id, artist.Name, artist.Releases.OrderBy(r => r.Title).Select(r => r.Title).ToList()));
                    var removed = await db.RemoveArtistAsync(artist);
                    releaseIds.AddRange(removed.ReleaseIds);
                    blobNames.AddRange(removed.BlobNames);
                }
                else
                {
                    artist.Managers.Remove(artist.Managers.Single(m => m.Id == memberId));
                }
            }

            await db.JoinRequests
                .Where(r => r.NormalizedEmail == member.NormalizedEmail)
                .ExecuteDeleteAsync();
            // Playlists, claims, logins, tokens, roles and passkeys cascade with the Member.
            db.Users.Remove(member);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            deleted = new DeletedMember(member.Email!, deletedArtists);
        });

        if (deleted is null)
        {
            return new MemberDeletionResult(MemberDeletionOutcome.NotFound);
        }

        try
        {
            // Play links they were handed must stop working, whatever went with them.
            await stores.CleanUpAsync(new Removed(releaseIds, blobNames, AnyPublished: true));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Couldn't clean up after deleting Member {Id}.", memberId);
        }
        return new MemberDeletionResult(MemberDeletionOutcome.Deleted, deleted);
    }
}
