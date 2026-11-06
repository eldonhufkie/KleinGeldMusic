using System.Security.Claims;
using KleinGeld.Contracts;
using KleinGeld.Data;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web.Api;

internal static class ReleaseLock
{
    /// <summary>
    /// <see cref="WithManagedReleaseLockedAsync"/> for changes the Submitted lock forbids: the Admin reviews exactly
    /// what was submitted, so a Submitted Release stays as it is until it's reviewed or withdrawn.
    /// </summary>
    public static Task<IResult?> WithUnsubmittedReleaseLockedAsync(
        this KleinGeldDbContext db, Guid id, ClaimsPrincipal caller, Func<Release, Task<IResult?>> change) =>
        db.WithManagedReleaseLockedAsync(id, caller, release =>
            release.Stage != ReleaseStage.Submitted
                ? change(release)
                : Task.FromResult<IResult?>(Results.Problem(
                    "A Submitted Release can't be changed. Withdraw it first.",
                    statusCode: StatusCodes.Status409Conflict)));

    /// <summary>
    /// Changes a Release while holding a lock on it, so a change can't slip past a stage or Track-count check that
    /// another request is about to invalidate. Answers "not found" to anyone who doesn't manage the Artist.
    /// <paramref name="change"/> returns null when it succeeded; anything else rolls back.
    /// </summary>
    public static Task<IResult?> WithManagedReleaseLockedAsync(
        this KleinGeldDbContext db, Guid id, ClaimsPrincipal caller, Func<Release, Task<IResult?>> change) =>
        // Retries run the whole transaction again, so nothing tracked by a failed attempt may carry over.
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM "Releases" WHERE "Id" = {id} FOR UPDATE""");

            var release = await db.Releases
                .Include(r => r.Artist)
                .Include(r => r.Tracks)
                .Where(r => r.Id == id)
                .ManagedBy(caller.MemberId())
                .SingleOrDefaultAsync();
            if (release is null)
            {
                return Results.NotFound();
            }

            var refused = await change(release);
            if (refused is null)
            {
                await transaction.CommitAsync();
            }
            return refused;
        });
}
