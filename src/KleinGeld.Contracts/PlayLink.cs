namespace KleinGeld.Contracts;

/// <summary>
/// A short-lived read link to a Track's audio, as returned by <c>GET /api/tracks/{id}/play-link</c>. Never stored:
/// the player asks for a new one each time a Track starts.
/// </summary>
public sealed record PlayLink(Uri Url, DateTimeOffset ExpiresOn);
