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

    /// <summary>Added to a Published Release as part of its Pending Changes, so only its managers and the Admin see it.</summary>
    public bool Pending { get; set; }

    public TrackStatus Status { get; set; } = TrackStatus.Uploading;

    /// <summary>Where the uploaded file is. Blob names are write-once: new audio gets a new name.</summary>
    public required string OriginalBlobName { get; set; }

    /// <summary>Where the streamable <c>.m4a</c> is, once the Track is Ready.</summary>
    public string? ConvertedBlobName { get; set; }

    /// <summary>Known once the Track is Ready.</summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>The upload under way replaces Failed audio, so a rejected file returns the Track to Failed.</summary>
    public bool IsReplacingFailedAudio { get; set; }

    /// <summary>Removed from a Published Release in its Pending Changes; Members keep it until they are approved.</summary>
    public bool PendingRemoval { get; set; }

    /// <summary>
    /// The audio Members hear while the new audio of a replaced Track awaits review in Pending Changes; null when the
    /// Track isn't replaced. The other blob and duration properties then hold the new audio.
    /// </summary>
    public string? LiveOriginalBlobName { get; set; }

    public string? LiveConvertedBlobName { get; set; }

    public TimeSpan? LiveDuration { get; set; }

    /// <summary>Its new audio awaits review in Pending Changes; until then Members hear the live audio.</summary>
    public bool IsReplaced => LiveConvertedBlobName is not null;

    /// <summary>Whether it is part of its Release's Pending Changes: added, removed or replaced.</summary>
    public bool IsPendingChange => Pending || PendingRemoval || IsReplaced;

    /// <summary>Every blob holding this Track's audio, live or new.</summary>
    public IEnumerable<string> BlobNames() =>
        new[] { OriginalBlobName, ConvertedBlobName, LiveOriginalBlobName, LiveConvertedBlobName }.OfType<string>();

    /// <summary>Puts the live audio aside for Members while new audio goes through upload and conversion.</summary>
    public void KeepLiveAudio()
    {
        LiveOriginalBlobName = OriginalBlobName;
        LiveConvertedBlobName = ConvertedBlobName;
        LiveDuration = Duration;
    }

    /// <summary>Gives up the new audio and makes the live audio the Track's audio again, Ready as it was.</summary>
    public void RestoreLiveAudio()
    {
        OriginalBlobName = LiveOriginalBlobName!;
        ConvertedBlobName = LiveConvertedBlobName;
        Duration = LiveDuration;
        Status = TrackStatus.Ready;
        IsReplacingFailedAudio = false;
        LiveOriginalBlobName = null;
        LiveConvertedBlobName = null;
        LiveDuration = null;
    }

    /// <summary>Lets the new audio become the Track's only audio; returns the blobs of the live audio it replaces.</summary>
    public IReadOnlyList<string> DropLiveAudio()
    {
        string?[] dropped = [LiveOriginalBlobName, LiveConvertedBlobName];
        LiveOriginalBlobName = null;
        LiveConvertedBlobName = null;
        LiveDuration = null;
        return dropped.OfType<string>().ToList();
    }
}
