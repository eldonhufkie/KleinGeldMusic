namespace KleinGeld.Contracts;

/// <summary>How far a Track's audio has come, from the browser's upload to being playable.</summary>
public enum TrackStatus
{
    Uploading,
    Converting,
    Ready,
    Failed,
}
