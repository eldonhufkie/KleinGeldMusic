namespace KleinGeld.Contracts;

/// <summary>How many Unavailable Tracks <c>DELETE /api/playlists/{id}/unavailable</c> took off the Playlist.</summary>
public sealed record RemovedUnavailableTracks(int Removed);
