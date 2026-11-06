namespace KleinGeld.Contracts;

/// <summary>An Artist's page in My Artists, as returned by <c>GET /api/my-artists/{id}/{slug}</c>.</summary>
public sealed record ManagedArtist(
    Guid Id,
    string Name,
    string Slug,
    IReadOnlyList<ArtistManager> Managers,
    IReadOnlyList<ManagedRelease> Releases);

/// <summary>A Member who manages an Artist.</summary>
public sealed record ArtistManager(Guid Id, string Name);

/// <summary>A Release on an Artist's page in My Artists, with the stage its badge shows.</summary>
public sealed record ManagedRelease(Guid Id, string Title, string Slug, ReleaseType Type, DateOnly? ReleaseDate, ReleaseStage Stage);
