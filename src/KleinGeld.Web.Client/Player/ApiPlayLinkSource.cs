using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Web.Client.Player;

/// <summary>Asks the API, on the same origin with the login cookie, for a Track's play link.</summary>
public sealed class ApiPlayLinkSource(HttpClient http) : IPlayLinkSource
{
    public async Task<Uri?> ForTrackAsync(Guid trackId)
    {
        try
        {
            using var response = await http.GetAsync($"api/tracks/{trackId}/play-link");
            return response.IsSuccessStatusCode
                ? (await response.Content.ReadFromJsonAsync<PlayLink>())!.Url
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
