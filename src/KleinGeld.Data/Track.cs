using KleinGeld.Contracts;

namespace KleinGeld.Data;

/// <summary>One piece of audio on exactly one Release.</summary>
public sealed class Track
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid ReleaseId { get; init; }

    public Release Release { get; init; } = null!;

    public required string Title { get; set; }

    /// <summary>The Track number, from 1, without gaps.</summary>
    public required int Position { get; set; }

    public TrackStatus Status { get; set; } = TrackStatus.Uploading;

    /// <summary>Where the uploaded file is. Blob names are write-once: new audio gets a new name.</summary>
    public required string OriginalBlobName { get; set; }
}
