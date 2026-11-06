using KleinGeld.Contracts;

namespace KleinGeld.Web.Client.Player;

/// <summary>Where the player gets a Track's play link. The API in the browser, a fake in tests.</summary>
public interface IPlayLinkSource
{
    /// <summary>A fresh play link, or null when the Track is refused: it can't be played.</summary>
    /// <exception cref="PlayLinkFailedException">There was no answer, so the Track may well still be playable.</exception>
    Task<PlayLink?> ForTrackAsync(Guid trackId);
}
