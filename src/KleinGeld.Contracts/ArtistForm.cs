using System.ComponentModel.DataAnnotations;

namespace KleinGeld.Contracts;

/// <summary>
/// The name of an Artist, as posted to <c>POST /api/artists</c> and <c>PUT /api/artists/{id}</c>. The same rules
/// validate the form in the browser and the request on the server.
/// </summary>
public sealed class ArtistForm
{
    public const int NameMaxLength = 100;

    [Required, StringLength(NameMaxLength)]
    public string Name { get; set; } = "";
}
