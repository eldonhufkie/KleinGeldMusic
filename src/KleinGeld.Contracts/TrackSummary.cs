namespace KleinGeld.Contracts;

/// <summary>A Track as its Release lists it. <paramref name="Duration"/> is known once the Track is Ready.</summary>
public sealed record TrackSummary(Guid Id, string Title, int Position, TrackStatus Status, TimeSpan? Duration = null);
