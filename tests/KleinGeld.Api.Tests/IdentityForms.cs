using System.Text.RegularExpressions;

namespace KleinGeld.Api.Tests;

/// <summary>Reads the hidden fields (antiforgery token, form handler) of the server-rendered Identity pages.</summary>
internal static partial class IdentityForms
{
    public static async Task<Dictionary<string, string>> ReadAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        return HiddenInput().Matches(html)
            .Select(match => (Name: match.Groups["name"].Value, Value: WebUtility.HtmlDecode(match.Groups["value"].Value)))
            .DistinctBy(field => field.Name)
            .ToDictionary(field => field.Name, field => field.Value);
    }

    [GeneratedRegex("""<input[^>]*type="hidden"[^>]*name="(?<name>[^"]+)"[^>]*value="(?<value>[^"]*)"[^>]*>""")]
    private static partial Regex HiddenInput();
}
