using System.Globalization;
using System.Text;

namespace KleinGeld.Web;

/// <summary>Turns names into the readable part of a URL (ADR 0002).</summary>
internal static class Slugs
{
    /// <summary>The slug of a name with no letters or digits to use, such as "???".</summary>
    private const string Fallback = "untitled";

    /// <summary>Lower-cases the name, drops accents and joins what's left with hyphens: "De Kapél!" gives "de-kapel".</summary>
    public static string From(string name)
    {
        var slug = new StringBuilder(name.Length);
        foreach (var character in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(char.ToLowerInvariant(character));
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var trimmed = slug.ToString().TrimEnd('-');
        return trimmed.Length > 0 ? trimmed : Fallback;
    }
}
