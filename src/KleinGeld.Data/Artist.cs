namespace KleinGeld.Data;

/// <summary>A public name music is published under, managed by one or more Members (a solo act or a band).</summary>
public sealed class Artist
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    /// <summary>The readable part of the Artist's URLs. It follows <see cref="Name"/>; the id finds the record.</summary>
    public required string Slug { get; set; }

    /// <summary>The Members who manage this Artist, all equal.</summary>
    public ICollection<Member> Managers { get; init; } = [];

    public ICollection<Release> Releases { get; init; } = [];
}
