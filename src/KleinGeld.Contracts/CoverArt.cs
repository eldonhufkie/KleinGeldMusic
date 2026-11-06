namespace KleinGeld.Contracts;

/// <summary>Short-lived links to the ~300 px and ~1000 px copies of a Release's cover art.</summary>
public sealed record CoverArt(Uri Small, Uri Large);
