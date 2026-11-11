using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Api;
using KleinGeld.Web.Client.Playlists;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web;

/// <summary>Lists the Member's Playlists for the sidebar on Identity's static pages, straight from the database.</summary>
internal sealed class ServerPlaylistsState(IHttpContextAccessor http, IServiceScopeFactory scopes) : IPlaylistsState
{
    public IReadOnlyList<PlaylistSummary> Playlists { get; private set; } = [];

    // A static page renders once, so nothing ever changes while it's on screen.
    public event Action? Changed { add { } remove { } }

    public async Task LoadAsync()
    {
        var member = http.HttpContext?.User;
        if (member?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // Its own scope: the page beside the sidebar is using the request's DbContext at the same moment.
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KleinGeldDbContext>();
        var memberId = member.MemberId();
        Playlists = await db.Playlists
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.ChangedAt)
            .ThenByDescending(p => p.Id)
            .Select(p => new PlaylistSummary(p.Id, p.Name, p.Slug, p.Entries.Count, p.ChangedAt))
            .ToListAsync();
    }
}
