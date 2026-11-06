namespace KleinGeld.Contracts;

/// <summary>The answer to confirming an upload. <paramref name="Note"/> says what pre-filling had to leave out, if anything.</summary>
public sealed record TrackConfirmation(TrackSummary Track, string? Note = null);
