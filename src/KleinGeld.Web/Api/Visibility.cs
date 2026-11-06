using System.Linq.Expressions;
using System.Security.Claims;
using KleinGeld.Contracts;
using KleinGeld.Data;

namespace KleinGeld.Web.Api;

/// <summary>
/// The visibility rule, in one place: every Member sees Published Releases and the Artists that have one. Anything
/// else is seen only by the Artist's managing Members and the Admin, and answers "not found" to everyone else.
/// </summary>
internal static class Visibility
{
    /// <summary>What every Member sees of an Artist's Releases, for lists that are the same whoever asks.</summary>
    public static readonly Expression<Func<Release, bool>> IsPublished = r => r.Stage == ReleaseStage.Published;

    /// <summary>The Artists every Member sees, for lists that are the same whoever asks.</summary>
    public static IQueryable<Artist> WithPublishedRelease(this IQueryable<Artist> artists) =>
        artists.Where(a => a.Releases.AsQueryable().Any(IsPublished));

    public static IQueryable<Release> VisibleTo(this IQueryable<Release> releases, ClaimsPrincipal caller)
    {
        if (caller.IsInRole(Roles.Admin))
        {
            return releases;
        }

        var memberId = caller.MemberId();
        return releases.Where(r => r.Stage == ReleaseStage.Published || r.Artist.Managers.Any(m => m.Id == memberId));
    }

    public static IQueryable<Artist> VisibleTo(this IQueryable<Artist> artists, ClaimsPrincipal caller)
    {
        if (caller.IsInRole(Roles.Admin))
        {
            return artists;
        }

        var memberId = caller.MemberId();
        return artists.Where(a =>
            a.Releases.Any(r => r.Stage == ReleaseStage.Published) || a.Managers.Any(m => m.Id == memberId));
    }
}
