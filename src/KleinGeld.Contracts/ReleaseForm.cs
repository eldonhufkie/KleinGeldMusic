using System.ComponentModel.DataAnnotations;

namespace KleinGeld.Contracts;

/// <summary>
/// The details of a Release, as posted to <c>POST /api/artists/{id}/releases</c> and <c>PUT /api/releases/{id}</c>.
/// The same rules validate the form in the browser and the request on the server.
/// </summary>
public sealed class ReleaseForm
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int GenreMaxLength = 100;

    [StringLength(TitleMaxLength)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Choose a Release type."), EnumDataType(typeof(ReleaseType))]
    public ReleaseType? Type { get; set; }

    /// <summary>Display and sorting only, so it may be in the past. Blank is allowed on a Draft, like the title, for tags to fill.</summary>
    public DateOnly? ReleaseDate { get; set; }

    [StringLength(DescriptionMaxLength)]
    public string? Description { get; set; }

    [StringLength(GenreMaxLength)]
    public string? Genre { get; set; }
}
