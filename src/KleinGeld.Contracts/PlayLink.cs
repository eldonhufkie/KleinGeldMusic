namespace KleinGeld.Contracts;

/// <summary>
/// A short-lived read link to a Track's audio, as returned by <c>GET /api/tracks/{id}/play-link</c>. Never stored:
/// the player asks for a new one each time a Track starts. <paramref name="Cover"/> comes with it so the lock screen
/// gets links as fresh as the audio's; it is null when the Release has no cover art yet.
/// </summary>
public sealed record PlayLink(Uri Url, DateTimeOffset ExpiresOn, CoverArt? Cover = null);
