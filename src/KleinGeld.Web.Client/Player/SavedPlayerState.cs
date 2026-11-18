namespace KleinGeld.Web.Client.Player;

/// <summary>What survives a reload. Missing fields read as their defaults, so older saved state still loads.</summary>
public sealed record SavedPlayerState(
    List<QueueEntry> Queue,
    int CurrentIndex,
    TimeSpan Position,
    TimeSpan Duration,
    double Volume = 1,
    bool IsMuted = false,
    bool Shuffle = false,
    RepeatMode Repeat = RepeatMode.Off);

/// <summary>
/// A place in the Queue. <paramref name="SourceOrder"/> is where the Track was in the list Play was pressed on, and
/// null for one added by Play next or Add to queue, which shuffle leaves alone.
/// </summary>
public sealed record QueueEntry(PlayerTrack Track, int? SourceOrder);
