using KleinGeld.Contracts;

namespace KleinGeld.Web.Client.Player;

/// <summary>
/// What the player needs to know about a Track. It holds the id, never a link: the Queue is made of these, and a link
/// is fetched each time a Track starts.
/// </summary>
public sealed record PlayerTrack(Guid Id, string Title, string Artist, bool IsAvailable = true)
{
    public static PlayerTrack From(TrackSummary track, string artist) =>
        new(track.Id, track.Title, artist, track.Status == TrackStatus.Ready);
}
