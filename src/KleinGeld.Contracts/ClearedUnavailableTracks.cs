namespace KleinGeld.Contracts;

/// <summary>How many Unavailable Tracks <c>DELETE /api/playlists/{id}/unavailable</c> cleared off the Playlist.</summary>
public sealed record ClearedUnavailableTracks(int Cleared);
