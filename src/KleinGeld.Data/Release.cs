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

    /// <summary>Why the Admin last Rejected it, kept through a resubmission but only shown while it is Rejected.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// The stage of its one set of Pending Changes: Draft, Submitted or Rejected, or null when it has none. Only a
    /// Published Release has them; approving merges them into the Release.
    /// </summary>
    public ReleaseStage? PendingChanges { get; set; }

    /// <summary>Why the Admin last Rejected its Pending Changes; only shown while they are Rejected.</summary>
    public string? PendingChangesRejectionReason { get; set; }

    /// <summary>Names the blobs of the current cover art; null until one is uploaded.</summary>
    public Guid? CoverId { get; set; }

    public List<Track> Tracks { get; init; } = [];

    /// <summary>One per submission, so a resubmitted Release has several; the newest dates the current one.</summary>
    public List<RightsDeclaration> RightsDeclarations { get; init; } = [];

    /// <summary>
    /// Makes the Pending Changes part of the Release, as approving them does, or as a Takedown or Reject does so the
    /// Release is reviewed whole on resubmission: added Tracks stay, removed Tracks go and replaced Tracks keep only
    /// their new audio. Returns the blobs no Track uses any more, for deleting once this is saved.
    /// </summary>
    public IReadOnlyList<string> MergePendingChanges()
    {
        List<string> unused = [];
        foreach (var removed in Tracks.Where(t => t.PendingRemoval).ToList())
        {
            unused.AddRange(removed.BlobNames());
            Tracks.Remove(removed);
        }
        foreach (var track in Tracks)
        {
            track.Pending = false;
            unused.AddRange(track.DropLiveAudio());
        }
        var position = 1;
        foreach (var track in Tracks.OrderBy(t => t.Position))
        {
            track.Position = position++;
        }
        PendingChanges = null;
        PendingChangesRejectionReason = null;
        return unused;
    }
}
