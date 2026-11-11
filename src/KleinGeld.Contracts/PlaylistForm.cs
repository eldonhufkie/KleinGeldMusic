using System.ComponentModel.DataAnnotations;

namespace KleinGeld.Contracts;

/// <summary>
/// The name of a Playlist, as posted to <c>POST /api/playlists</c>. Duplicate names are allowed. The same rules
/// validate the form in the browser and the request on the server.
/// </summary>
public sealed class PlaylistForm
{
    public const int NameMaxLength = 100;

    [Required, StringLength(NameMaxLength)]
    public string Name { get; set; } = "";
}
