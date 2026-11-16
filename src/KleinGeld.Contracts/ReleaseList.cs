namespace KleinGeld.Contracts;

/// <summary>One page of the Releases grid. <see cref="Next"/> asks for the following page; null on the last one.</summary>
public sealed record ReleaseList(IReadOnlyList<ReleaseCard> Items, string? Next);
