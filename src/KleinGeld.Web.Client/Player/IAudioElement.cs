namespace KleinGeld.Web.Client.Player;

/// <summary>
/// The browser's single <c>&lt;audio&gt;</c> element, driven through a small JS module.
/// The seam that lets <see cref="PlayerService"/> be tested without a browser.
/// </summary>
public interface IAudioElement
{
    /// <summary>Raised as the audio moves on, and when it starts or stops by itself.</summary>
    event Action<AudioProgress>? Progressed;

    Task PlayAsync(string source);

    Task PauseAsync();

    Task ResumeAsync();

    Task SeekAsync(TimeSpan position);
}

/// <summary>Where the audio element is. <paramref name="Duration"/> is zero until the browser knows it.</summary>
public sealed record AudioProgress(TimeSpan Position, TimeSpan Duration, bool IsPlaying);
