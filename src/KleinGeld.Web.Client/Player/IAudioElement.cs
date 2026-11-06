using KleinGeld.Contracts;

namespace KleinGeld.Web.Client.Player;

/// <summary>
/// The browser's single <c>&lt;audio&gt;</c> element, driven through a small JS module.
/// The seam that lets <see cref="PlayerService"/> be tested without a browser.
/// </summary>
public interface IAudioElement
{
    /// <summary>Raised as the audio moves on, and when it starts or stops by itself.</summary>
    event Action<AudioProgress>? Progressed;

    /// <summary>Raised when a media key, headphone button or the lock screen asks for something.</summary>
    event Func<MediaCommand, Task>? MediaCommanded;

    /// <summary>Raised when a Track plays to its end by itself.</summary>
    event Func<Task>? Ended;

    Task PlayAsync(string source);

    Task PauseAsync();

    Task ResumeAsync();

    Task SeekAsync(TimeSpan position);

    /// <summary>Puts a Track on the lock screen and the browser's media controls.</summary>
    Task ShowMetadataAsync(MediaMetadata metadata);
}

/// <summary>What the Member pressed outside the page. <paramref name="Position"/> is where a Seek goes.</summary>
public sealed record MediaCommand(MediaAction Action, TimeSpan Position = default);

public enum MediaAction
{
    Play,
    Pause,
    Next,
    Previous,
    Seek,
}

/// <summary>What the Media Session shows for the playing Track.</summary>
public sealed record MediaMetadata(string Title, string Artist, CoverArt? Cover);

/// <summary>Where the audio element is. <paramref name="Duration"/> is zero until the browser knows it.</summary>
public sealed record AudioProgress(TimeSpan Position, TimeSpan Duration, bool IsPlaying);
