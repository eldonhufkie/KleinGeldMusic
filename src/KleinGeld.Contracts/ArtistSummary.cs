namespace KleinGeld.Contracts;

/// <summary>An Artist with what it takes to link to it, as listed by <c>GET /api/my-artists</c>.</summary>
public sealed record ArtistSummary(Guid Id, string Name, string Slug);
