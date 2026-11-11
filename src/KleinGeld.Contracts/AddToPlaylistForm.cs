namespace KleinGeld.Contracts;

/// <summary>The Track to append to a Playlist, as posted to <c>POST /api/playlists/{id}/tracks</c>.</summary>
public sealed class AddToPlaylistForm
{
    public Guid TrackId { get; set; }
}
