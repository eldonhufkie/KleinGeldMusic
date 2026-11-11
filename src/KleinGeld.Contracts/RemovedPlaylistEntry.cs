namespace KleinGeld.Contracts;

/// <summary>
/// A Track just removed from a Playlist, as returned by <c>DELETE /api/playlists/{id}/entries/{entryId}</c>. Posting it
/// back to <c>POST /api/playlists/{id}/entries/restore</c> is the Undo: it goes back in the same place. Only the Track
/// and position are used; the title and Artist come from the Track, so an Unavailable Track can't be put back.
/// </summary>
public sealed record RemovedPlaylistEntry(Guid? TrackId, string Title, string ArtistName, int Position);
