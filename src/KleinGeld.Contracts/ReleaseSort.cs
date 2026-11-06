using System.Text.Json.Serialization;

namespace KleinGeld.Contracts;

/// <summary>How the Releases grid is sorted: Newest by Published date, or A–Z by title.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReleaseSort>))]
public enum ReleaseSort
{
    Newest,
    AZ,
}
