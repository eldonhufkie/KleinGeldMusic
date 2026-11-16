using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Web.Client.Artists;

/// <summary>
/// Whether the logged-in Member manages an Artist, which decides if the sidebar shows My Artists. The sidebar also
/// renders on the server for Identity's static pages, so the host has its own implementation.
/// </summary>
public interface IManagedArtistsState
{
    bool ManagesArtist { get; }

    event Action? Changed;

    Task LoadAsync();
}

/// <summary>Asks the API, and tells the sidebar when the answer may have changed.</summary>
public sealed class ManagedArtistsState(HttpClient http) : IManagedArtistsState
{
    public bool ManagesArtist { get; private set; }

    public event Action? Changed;

    public async Task LoadAsync()
    {
        var me = await http.GetFromJsonAsync<MeResponse>("api/me");
        ManagesArtist = me?.ManagesArtist ?? false;
        Changed?.Invoke();
    }
}
