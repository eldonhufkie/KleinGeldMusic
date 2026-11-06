using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Web.Client.Player;

/// <summary>Asks the API, on the same origin with the login cookie, for a Track's play link.</summary>
public sealed class ApiPlayLinkSource(HttpClient http) : IPlayLinkSource
{
    public async Task<PlayLink?> ForTrackAsync(Guid trackId)
    {
        try
        {
            using var response = await http.GetAsync($"api/tracks/{trackId}/play-link");
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PlayLink>();
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or NotSupportedException)
        {
            throw new PlayLinkFailedException($"No answer about Track {trackId}.", e);
        }
    }
}
