using System.Text.Json.Serialization;

namespace KleinGeld.Contracts;

/// <summary>Where a Release is on its way to Members. Every Release starts as a Draft.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReleaseStage>))]
public enum ReleaseStage
{
    Draft,
    Submitted,
    Published,
    Rejected,

    /// <summary>Hidden from other Members by its own Artist after being Published; it can be resubmitted.</summary>
    TakenDown,
}
