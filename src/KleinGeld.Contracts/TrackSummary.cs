namespace KleinGeld.Contracts;

/// <summary>A Track on a Release's Track list, as returned by <c>GET /api/tracks?releaseId={id}</c>.</summary>
public sealed record TrackSummary(Guid Id, string Title, int Position, TrackStatus Status);
