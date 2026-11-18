namespace KleinGeld.Data;

/// <summary>A Member's personal, ordered list of Tracks drawn from any Releases; private to that Member.</summary>
public sealed class Playlist
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid MemberId { get; init; }

    public required string Name { get; set; }

    /// <summary>The readable part of the Playlist's URLs. It follows <see cref="Name"/>; the id finds the record.</summary>
    public required string Slug { get; set; }

    /// <summary>Bumped by every change to the Playlist, which orders the Member's list.</summary>
    public DateTimeOffset ChangedAt { get; set; }

    public List<PlaylistEntry> Entries { get; init; } = [];
}

/// <summary>A Track on a Playlist, with a snapshot of its title and Artist name taken when it was added.</summary>
public sealed class PlaylistEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid PlaylistId { get; init; }

    /// <summary>Null once the Track is deleted, so the entry survives as an Unavailable Track.</summary>
    public Guid? TrackId { get; set; }

    public Track? Track { get; init; }

    /// <summary>From 1, without gaps.</summary>
    public required int Position { get; set; }

    public required string Title { get; init; }

    public required string ArtistName { get; init; }
}
