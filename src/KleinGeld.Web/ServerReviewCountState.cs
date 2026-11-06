using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Api;
using KleinGeld.Web.Client.Artists;

namespace KleinGeld.Web;

/// <summary>Answers for the sidebar on Identity's static pages, straight from the database.</summary>
internal sealed class ServerReviewCountState(IHttpContextAccessor http, IServiceScopeFactory scopes) : IReviewCountState
{
    public int Count { get; private set; }

    // A static page renders once, so nothing ever changes while it's on screen.
    public event Action? Changed { add { } remove { } }

    public async Task LoadAsync()
    {
        if (http.HttpContext?.User.IsInRole(Roles.Admin) != true)
        {
            return;
        }

        // Its own scope: the page beside the sidebar is using the request's DbContext at the same moment.
        await using var scope = scopes.CreateAsyncScope();
        Count = await scope.ServiceProvider.GetRequiredService<KleinGeldDbContext>().AwaitingReviewCountAsync();
    }
}
