namespace KleinGeld.Web.Client.Player;

/// <summary>
/// Holds the playback state for this browser tab and raises <see cref="Changed"/> whenever it changes.
/// </summary>
public sealed class PlayerService
{
    private readonly IAudioElement _audio;
    private readonly IPlayLinkSource _links;

    public PlayerService(IAudioElement audio, IPlayLinkSource links)
    {
        _audio = audio;
        _links = links;
        _audio.Progressed += progress =>
        {
            Position = progress.Position;
            Duration = progress.Duration;
            IsPlaying = progress.IsPlaying;
            Changed?.Invoke();
        };
    }

    public event Action? Changed;

    public PlayerTrack? Current { get; private set; }

    public bool IsPlaying { get; private set; }

    public TimeSpan Position { get; private set; }

    public TimeSpan Duration { get; private set; }

    /// <summary>A short message for the Member about what just went wrong, if anything did.</summary>
    public string? Notice { get; private set; }

    /// <summary>Whether a phone shows the full player over the page rather than the mini player.</summary>
    public bool IsFullPlayerOpen { get; private set; }

    public void OpenFullPlayer()
    {
        IsFullPlayerOpen = true;
        Changed?.Invoke();
    }

    public void CloseFullPlayer()
    {
        IsFullPlayerOpen = false;
        Changed?.Invoke();
    }

    public async Task PlayAsync(PlayerTrack track)
    {
        var link = await _links.ForTrackAsync(track.Id);
        if (link is null)
        {
            Notice = $"{track.Title} can't be played right now.";
            Changed?.Invoke();
            return;
        }

        await _audio.PlayAsync(link.ToString());
        Current = track;
        Notice = null;
        Position = TimeSpan.Zero;
        Duration = TimeSpan.Zero;
        IsPlaying = true;
        Changed?.Invoke();
    }

    public async Task PauseAsync()
    {
        await _audio.PauseAsync();
        IsPlaying = false;
        Changed?.Invoke();
    }

    public async Task ResumeAsync()
    {
        await _audio.ResumeAsync();
        IsPlaying = true;
        Changed?.Invoke();
    }

    public async Task SeekAsync(TimeSpan position)
    {
        await _audio.SeekAsync(position);
        Position = position;
        Changed?.Invoke();
    }
}
