namespace KleinGeld.Contracts;

/// <summary>Where a Playlist entry moves to, from 1, as put to <c>PUT /api/playlists/{id}/entries/{entryId}/position</c>.</summary>
public sealed class MovePlaylistEntryForm
{
    public int Position { get; set; }
}
