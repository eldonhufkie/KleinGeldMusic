using System.ComponentModel.DataAnnotations;

namespace KleinGeld.Contracts;

/// <summary>The details of a Track, as put to <c>PUT /api/releases/{id}/tracks/{trackId}</c>.</summary>
public sealed class TrackForm
{
    public const int FeaturedArtistsMaxLength = 200;

    [Required, StringLength(ReleaseForm.TitleMaxLength)]
    public string Title { get; set; } = "";

    public bool SingleMark { get; set; }

    [StringLength(FeaturedArtistsMaxLength)]
    public string? FeaturedArtists { get; set; }

    public bool Explicit { get; set; }
}
