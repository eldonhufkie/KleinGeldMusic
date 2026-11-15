namespace KleinGeld.Contracts;

/// <summary>
/// What deleting a Member deletes with them, as returned by <c>GET /api/me/leave</c> and shown again when the Admin
/// Removes them: their Playlists, and the Artists only they manage with those Artists' Releases. Artists they share
/// stay with the other managers.
/// </summary>
public sealed record DeletionPreview(
    IReadOnlyList<string> Playlists,
    IReadOnlyList<DeletedArtist> Artists,
    string BackupNotice)
{
    public const string BackupWording = "Backups containing your data are erased within 30 days.";
}

/// <summary>An Artist deleted with its only managing Member, and the titles of its Releases.</summary>
public sealed record DeletedArtist(Guid Id, string Name, IReadOnlyList<string> Releases);
