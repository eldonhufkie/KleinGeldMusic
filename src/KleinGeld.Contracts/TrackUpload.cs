namespace KleinGeld.Contracts;

/// <summary>A new Track and the short-lived link its file is uploaded to, straight to blob storage.</summary>
public sealed record TrackUpload(TrackSummary Track, Uri UploadLink);
