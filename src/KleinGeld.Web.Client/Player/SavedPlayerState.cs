namespace KleinGeld.Web.Client.Player;

/// <summary>What survives a reload. Missing fields read as their defaults, so older saved state still loads.</summary>
public sealed record SavedPlayerState(
    List<PlayerTrack> Queue,
    int CurrentIndex,
    TimeSpan Position,
    TimeSpan Duration,
    double Volume = 1,
    bool IsMuted = false,
    bool IsShuffled = false,
    string Repeat = "Off");
