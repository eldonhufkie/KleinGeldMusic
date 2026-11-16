namespace KleinGeld.Contracts;

/// <summary>
/// What a Release needs before it can be Submitted. The Release editor disables Submit with these reasons, and the
/// API refuses with them.
/// </summary>
public static class SubmitRules
{
    /// <summary>Why a Release can't be Submitted yet, or null when it can.</summary>
    public static string? Refusal(string title, DateOnly? releaseDate, bool hasCover, IReadOnlyCollection<TrackStatus> tracks)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Give the Release a title first.";
        }
        if (releaseDate is null)
        {
            return "Give the Release a release date first.";
        }
        if (!hasCover)
        {
            return "Add the cover art first.";
        }
        if (tracks.Count == 0)
        {
            return "A Release needs at least one Track.";
        }
        if (tracks.Any(status => status != TrackStatus.Ready))
        {
            return "Wait until every Track is Ready, and replace or remove any that Failed.";
        }
        return null;
    }
}
