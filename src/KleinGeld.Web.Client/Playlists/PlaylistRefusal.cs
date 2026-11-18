using System.Net.Http.Json;
using System.Text.Json;

namespace KleinGeld.Web.Client.Playlists;

/// <summary>The reason a Playlist request was refused, as the API words it, for showing in a menu.</summary>
internal static class PlaylistRefusal
{
    public static async Task<string> ReadAsync(HttpResponseMessage response, string fallback)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            return problem.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } text
                ? text.TrimEnd('.')
                : fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}
