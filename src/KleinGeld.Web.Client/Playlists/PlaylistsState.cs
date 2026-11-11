using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Web.Client.Playlists;

/// <summary>
/// The logged-in Member's Playlists, most recently changed first, for the sidebar, home and Add to Playlist menus.
/// The sidebar also renders on the server for Identity's static pages, so the host has its own implementation.
/// </summary>
public interface IPlaylistsState
{
    IReadOnlyList<PlaylistSummary> Playlists { get; }

    event Action? Changed;

    Task LoadAsync();
}

/// <summary>Asks the API, and tells its readers when the list may have changed.</summary>
public sealed class PlaylistsState(HttpClient http) : IPlaylistsState
{
    public IReadOnlyList<PlaylistSummary> Playlists { get; private set; } = [];

    public event Action? Changed;

    public async Task LoadAsync()
    {
        Playlists = await http.GetFromJsonAsync<List<PlaylistSummary>>("api/playlists") ?? [];
        Changed?.Invoke();
    }
}
