using KleinGeld.Data;
using KleinGeld.Web.Api;
using KleinGeld.Web.Client.Artists;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web;

/// <summary>Answers for the sidebar on Identity's static pages, straight from the database.</summary>
internal sealed class ServerManagedArtistsState(IHttpContextAccessor http, IServiceScopeFactory scopes) : IManagedArtistsState
{
    public bool ManagesArtist { get; private set; }

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
        ManagesArtist = await db.Artists.ManagedBy(member.MemberId()).AnyAsync();
    }
}
