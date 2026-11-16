using System.Text.Json.Serialization;

namespace KleinGeld.Contracts;

/// <summary>What kind of Release it is, chosen by its Artist and never worked out from the number of Tracks.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReleaseType>))]
public enum ReleaseType
{
    Album,
    EP,
    Single,
}
