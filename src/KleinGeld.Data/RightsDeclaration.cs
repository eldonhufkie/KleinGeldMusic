namespace KleinGeld.Data;

/// <summary>An Artist's statement, made at one submission of a Release, that they own the rights to its music.</summary>
public sealed class RightsDeclaration
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid ReleaseId { get; init; }

    /// <summary>Who ticked it. Null once that Member is gone, so that leaving doesn't erase the record.</summary>
    public Guid? MemberId { get; init; }

    public DateTimeOffset MadeAt { get; init; }

    public int WordingVersion { get; init; }
}
