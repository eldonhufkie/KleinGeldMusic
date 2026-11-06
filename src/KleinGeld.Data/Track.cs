using KleinGeld.Contracts;

namespace KleinGeld.Data;

/// <summary>One piece of audio on exactly one Release.</summary>
public sealed class Track
{
    /// <summary>The private blob container holding every Track's audio.</summary>
    public const string AudioContainer = "audio";

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid ReleaseId { get; init; }

    public Release Release { get; init; } = null!;

    public required string Title { get; set; }

    /// <summary>A Member changed the title, so the tag of the file must leave it alone.</summary>
    public bool TitleTyped { get; set; }

    /// <summary>The Track number, from 1, without gaps.</summary>
    public required int Position { get; set; }

    /// <summary>Whether the Track was also put out as a Single, whatever the Release type of its own Release.</summary>
    public bool SingleMark { get; set; }

    /// <summary>Free text: featured Artists needn't be Artists on KleinGeld Music.</summary>
    public string? FeaturedArtists { get; set; }

    public bool Explicit { get; set; }

    public TrackStatus Status { get; set; } = TrackStatus.Uploading;

    /// <summary>Where the uploaded file is. Blob names are write-once: new audio gets a new name.</summary>
    public required string OriginalBlobName { get; set; }

    /// <summary>Where the streamable <c>.m4a</c> is, once the Track is Ready.</summary>
    public string? ConvertedBlobName { get; set; }

    /// <summary>Known once the Track is Ready.</summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>The upload under way replaces Failed audio, so a rejected file returns the Track to Failed.</summary>
    public bool IsReplacingFailedAudio { get; set; }

    /// <summary>Every blob holding this Track's audio.</summary>
    public IEnumerable<string> BlobNames() =>
        ConvertedBlobName is null ? [OriginalBlobName] : [OriginalBlobName, ConvertedBlobName];
}
