using System.Buffers.Text;
using System.Text.Json;

namespace KleinGeld.Web.Api;

/// <summary>Where a page of a keyset-paged list ended, as the opaque <c>after</c> value that asks for the next one.</summary>
internal static class PagingPosition
{
    public static string Format<T>(T position) => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(position));

    /// <summary>The position in <paramref name="text"/>, or null when a client sent anything else.</summary>
    public static T? Parse<T>(string text) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(Base64Url.DecodeFromChars(text));
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return null;
        }
    }
}
