// PROTOTYPE: in-memory sample data shared by both front-end variants. Throw away.
namespace Prototype;

public record Artist(int Id, string Name);
public record Release(int Id, int ArtistId, string Title, int Year);
public record Track(int Id, int ReleaseId, int Number, string Title, bool IsSingle, int Seconds, double BaseHz);

// Flat shape handed to the player, so it doesn't need to look anything up.
public record QueueItem(int TrackId, string Title, string ArtistName, int ReleaseId, string ReleaseTitle, bool IsSingle)
{
    public string AudioUrl => $"audio/{TrackId}.wav";
}

public static class Catalog
{
    public static readonly Artist[] Artists =
    [
        new(1, "De Kleingeldjes"),
        new(2, "Maartje & the Muntstukken"),
    ];

    public static readonly Release[] Releases =
    [
        new(1, 1, "Wisselgeld", 2025),
        new(2, 1, "Spaarpot EP", 2026),
        new(3, 2, "Kwartjes", 2026),
    ];

    public static readonly Track[] Tracks =
    [
        new(1, 1, 1, "Eerste Stuiver", false, 90, 220),
        new(2, 1, 2, "Dubbeltje Op Zijn Kant", true, 75, 247),
        new(3, 1, 3, "Rijksdaalder", false, 120, 196),
        new(4, 2, 1, "Spaarpot", false, 60, 262),
        new(5, 2, 2, "Muntjes Tellen", true, 80, 294),
        new(6, 3, 1, "Kwartje Van Kees", false, 100, 175),
        new(7, 3, 2, "Wisselkoers", false, 70, 330),
    ];

    public static Artist ArtistOf(Release r) => Artists.First(a => a.Id == r.ArtistId);
    public static IEnumerable<Release> ReleasesOf(Artist a) => Releases.Where(r => r.ArtistId == a.Id);
    public static IEnumerable<Track> TracksOf(Release r) => Tracks.Where(t => t.ReleaseId == r.Id).OrderBy(t => t.Number);

    public static QueueItem ToQueueItem(Track t)
    {
        var release = Releases.First(r => r.Id == t.ReleaseId);
        return new(t.Id, t.Title, ArtistOf(release).Name, release.Id, release.Title, t.IsSingle);
    }

    public static List<QueueItem> QueueFor(Release r) => TracksOf(r).Select(ToQueueItem).ToList();
}

// In variant A this lives in browser memory (lost on reload; the real app would call the API).
// In variant C it is one server-side singleton shared by everyone. Both are prototype shortcuts.
public class PlaylistStore
{
    readonly List<int> trackIds = [];
    public event Action? Changed;

    public void Add(int trackId) { trackIds.Add(trackId); Changed?.Invoke(); }
    public void RemoveAt(int index) { trackIds.RemoveAt(index); Changed?.Invoke(); }
    public List<QueueItem> Queue() => trackIds.Select(id => Catalog.ToQueueItem(Catalog.Tracks.First(t => t.Id == id))).ToList();
}
