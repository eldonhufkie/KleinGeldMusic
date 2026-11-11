namespace KleinGeld.Contracts;

/// <summary>A Playlist in the Member's list, as returned by <c>GET /api/playlists</c>, most recently changed first.</summary>
public sealed record PlaylistSummary(Guid Id, string Name, string Slug, int TrackCount, DateTimeOffset ChangedAt);
