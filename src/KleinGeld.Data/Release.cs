using KleinGeld.Contracts;

namespace KleinGeld.Data;

/// <summary>A collection of Tracks by an Artist, with one Release type.</summary>
public sealed class Release
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid ArtistId { get; init; }

    public Artist Artist { get; init; } = null!;

    public required string Title { get; set; }

    /// <summary>The readable part of the Release's URLs. It follows <see cref="Title"/>; the id finds the record.</summary>
    public required string Slug { get; set; }

    public required ReleaseType Type { get; set; }

    /// <summary>Display and sorting only, so it may be in the past. Null while a Draft leaves it blank.</summary>
    public DateOnly? ReleaseDate { get; set; }

    public string? Description { get; set; }

    public string? Genre { get; set; }

    public ReleaseStage Stage { get; set; } = ReleaseStage.Draft;

    /// <summary>When the Release was first approved; null until then, and a later approval leaves it as it is.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Why the Admin Rejected it; null unless it is Rejected, since a resubmission answers the reason.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Names the blobs of the current cover art; null until one is uploaded.</summary>
    public Guid? CoverId { get; set; }

    public List<Track> Tracks { get; init; } = [];

    /// <summary>One per submission, so a resubmitted Release has several; the newest dates the current one.</summary>
    public List<RightsDeclaration> RightsDeclarations { get; init; } = [];
}
