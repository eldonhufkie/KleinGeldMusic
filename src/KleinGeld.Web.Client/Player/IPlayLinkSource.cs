using KleinGeld.Contracts;

namespace KleinGeld.Web.Client.Player;

/// <summary>Where the player gets a Track's play link. The API in the browser, a fake in tests.</summary>
public interface IPlayLinkSource
{
    /// <summary>A fresh play link, or null when the Track can't be played.</summary>
    Task<PlayLink?> ForTrackAsync(Guid trackId);
}
