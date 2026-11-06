using KleinGeld.Web.Client.Player;

namespace KleinGeld.Web;

/// <summary>Lets the player bar render on Identity's static pages, where nothing can ever press play.</summary>
internal sealed class ServerPlayLinkSource : IPlayLinkSource
{
    public Task<Uri?> ForTrackAsync(Guid trackId) => Task.FromResult<Uri?>(null);
}
