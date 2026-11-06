using KleinGeld.Contracts;
using KleinGeld.Web.Client.Player;

namespace KleinGeld.Web;

/// <summary>Lets the player bar render on Identity's static pages, where nothing can ever press play.</summary>
internal sealed class ServerPlayLinkSource : IPlayLinkSource
{
    public Task<PlayLink?> ForTrackAsync(Guid trackId) => Task.FromResult<PlayLink?>(null);
}
