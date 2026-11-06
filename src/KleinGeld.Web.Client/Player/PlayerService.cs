using System.Text.Json;

namespace KleinGeld.Web.Client.Player;

/// <summary>
/// Holds the playback state for this browser tab and raises <see cref="Changed"/> whenever it changes.
/// </summary>
public sealed class PlayerService
{
    private static readonly TimeSpan RestartAfter = TimeSpan.FromSeconds(3);

    // The audio reports several times a second; a reload may lose this much of the position rather than write as often.
    private static readonly TimeSpan SavePositionEvery = TimeSpan.FromSeconds(5);

    private readonly IAudioElement _audio;
    private readonly IPlayLinkSource _links;
    private readonly IPlayerStorage _storage;

    private List<PlayerTrack> _queue = [];

    // Goes up with every press that starts a Track or shifts the Queue, so a play link that arrives late is dropped.
    private int _attempt;

    private TimeSpan _savedPosition;

    // After a reload the audio element is empty, and stays so until the Member presses play: a link fetched now could
    // expire long before that.
    private bool _awaitingFirstPlay;

    public PlayerService(IAudioElement audio, IPlayLinkSource links, IPlayerStorage storage)
    {
        _audio = audio;
        _links = links;
        _storage = storage;
        _audio.Progressed += progress =>
        {
            Position = progress.Position;
            Duration = progress.Duration;
            IsPlaying = progress.IsPlaying;
            if ((Position - _savedPosition).Duration() >= SavePositionEvery)
            {
                Save();
            }

            Changed?.Invoke();
        };
        _audio.MediaCommanded += command => command.Action switch
        {
            MediaAction.Play => ResumeAsync(),
            MediaAction.Pause => PauseAsync(),
            MediaAction.Next => NextAsync(),
            MediaAction.Previous => PreviousAsync(),
            MediaAction.Seek => SeekAsync(command.Position),
            _ => Task.CompletedTask,
        };
        _audio.Ended += NextAsync;
    }

    public event Action? Changed;

    /// <summary>The whole Queue: the Tracks already played, the current one and those still to come.</summary>
    public IReadOnlyList<PlayerTrack> Queue => _queue;

    /// <summary>Where <see cref="Current"/> is in the <see cref="Queue"/>, or -1 before anything has played.</summary>
    public int CurrentIndex { get; private set; } = -1;

    public PlayerTrack? Current => CurrentIndex >= 0 ? _queue[CurrentIndex] : null;

    public bool IsPlaying { get; private set; }

    public TimeSpan Position { get; private set; }

    public TimeSpan Duration { get; private set; }

    /// <summary>How loud the player is, from 0 to 1. Muting leaves it as it is.</summary>
    public double Volume { get; private set; } = 1;

    public bool IsMuted { get; private set; }

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

    /// <summary>Plays one Track on its own, replacing the Queue.</summary>
    public Task PlayAsync(PlayerTrack track) => PlayAsync([track], 0);

    /// <summary>
    /// Replaces the whole Queue with <paramref name="source"/>, in its order and without its Unavailable Tracks, and
    /// plays from the Track at <paramref name="start"/>.
    /// </summary>
    public Task PlayAsync(IReadOnlyList<PlayerTrack> source, int start)
    {
        if (!source[start].IsAvailable)
        {
            Refuse(source[start]);
            return Task.CompletedTask;
        }

        var queue = source.Where(track => track.IsAvailable).ToList();
        return StartAsync(queue, source.Take(start).Count(track => track.IsAvailable));
    }

    /// <summary>Puts a Track right after the current one. With nothing playing, it starts.</summary>
    public Task PlayNextAsync(PlayerTrack track)
    {
        if (!track.IsAvailable)
        {
            return Task.CompletedTask;
        }

        if (Current is null)
        {
            return PlayAsync(track);
        }

        _attempt++;
        _queue.Insert(CurrentIndex + 1, track);
        Save();
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>Puts a Track at the end of the Queue. With nothing playing, it starts.</summary>
    public Task AddToQueueAsync(PlayerTrack track)
    {
        if (!track.IsAvailable)
        {
            return Task.CompletedTask;
        }

        if (Current is null)
        {
            return PlayAsync(track);
        }

        _queue.Add(track);
        Save();
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>Takes the Track at <paramref name="index"/> out of the Queue. The current Track stays.</summary>
    public void RemoveFromQueue(int index)
    {
        if (index == CurrentIndex)
        {
            return;
        }

        _attempt++;
        _queue.RemoveAt(index);
        if (index < CurrentIndex)
        {
            CurrentIndex--;
        }

        Save();
        Changed?.Invoke();
    }

    public async Task PauseAsync()
    {
        await _audio.PauseAsync();
        IsPlaying = false;
        Save();
        Changed?.Invoke();
    }

    public async Task ResumeAsync()
    {
        if (_awaitingFirstPlay)
        {
            await PlayRestoredAsync();
            return;
        }

        await _audio.ResumeAsync();
        IsPlaying = true;
        Changed?.Invoke();
    }

    /// <summary>Moves on to the next Track, or stops on the last one.</summary>
    public async Task NextAsync()
    {
        if (Current is null)
        {
            return;
        }

        if (CurrentIndex + 1 < _queue.Count)
        {
            await StartAsync(_queue, CurrentIndex + 1);
        }
        else
        {
            await PauseAsync();
        }
    }

    /// <summary>Restarts the Track if it's more than 3 seconds in or the first in the Queue, otherwise goes back one.</summary>
    public async Task PreviousAsync()
    {
        if (Current is null)
        {
            return;
        }

        if (Position > RestartAfter || CurrentIndex == 0)
        {
            await SeekAsync(TimeSpan.Zero);
        }
        else
        {
            await StartAsync(_queue, CurrentIndex - 1);
        }
    }

    public async Task SeekAsync(TimeSpan position)
    {
        if (!_awaitingFirstPlay)
        {
            await _audio.SeekAsync(position);
        }

        Position = position;
        Save();
        Changed?.Invoke();
    }

    public async Task SetVolumeAsync(double volume)
    {
        await _audio.SetVolumeAsync(volume);
        Volume = volume;
        Save();
        Changed?.Invoke();
    }

    public async Task ToggleMuteAsync()
    {
        await _audio.SetMutedAsync(!IsMuted);
        IsMuted = !IsMuted;
        Save();
        Changed?.Invoke();
    }

    /// <summary>Picks up what this browser saved before the reload, paused.</summary>
    public async Task RestoreAsync()
    {
        var saved = await LoadAsync();
        if (saved is null)
        {
            return;
        }

        var attempt = _attempt;
        await _audio.SetVolumeAsync(saved.Volume);
        await _audio.SetMutedAsync(saved.IsMuted);
        Volume = saved.Volume;
        IsMuted = saved.IsMuted;

        var available = await AvailableAsync(saved.Queue);
        if (attempt == _attempt)
        {
            var isCurrentKept = saved.CurrentIndex >= 0 && available.Contains(saved.Queue[saved.CurrentIndex].Id);
            _queue = saved.Queue.Where(track => available.Contains(track.Id)).ToList();
            var keptBefore = saved.Queue.Take(saved.CurrentIndex).Count(track => available.Contains(track.Id));
            CurrentIndex = Math.Min(keptBefore, _queue.Count - 1);
            Position = _savedPosition = isCurrentKept ? saved.Position : TimeSpan.Zero;
            Duration = isCurrentKept ? saved.Duration : TimeSpan.Zero;
            _awaitingFirstPlay = Current is not null;
        }

        Changed?.Invoke();
    }

    private async Task StartAsync(List<PlayerTrack> queue, int index)
    {
        var attempt = ++_attempt;
        var track = queue[index];
        var link = await _links.ForTrackAsync(track.Id);
        if (attempt != _attempt)
        {
            return;
        }

        if (link is null)
        {
            Refuse(track);
            return;
        }

        _queue = queue;
        CurrentIndex = index;
        _awaitingFirstPlay = false;
        Notice = null;
        Position = TimeSpan.Zero;
        Duration = TimeSpan.Zero;
        IsPlaying = true;
        await _audio.ShowMetadataAsync(new MediaMetadata(track.Title, track.Artist, link.Cover));
        await _audio.PlayAsync(link.Url.ToString());
        Save();
        Changed?.Invoke();
    }

    private async Task PlayRestoredAsync()
    {
        var attempt = ++_attempt;
        var track = Current!;
        var link = await _links.ForTrackAsync(track.Id);
        if (attempt != _attempt)
        {
            return;
        }

        if (link is null)
        {
            Refuse(track);
            return;
        }

        _awaitingFirstPlay = false;
        Notice = null;
        IsPlaying = true;
        await _audio.ShowMetadataAsync(new MediaMetadata(track.Title, track.Artist, link.Cover));
        await _audio.PlayFromAsync(link.Url.ToString(), Position);
        Changed?.Invoke();
    }

    private async Task<SavedPlayerState?> LoadAsync()
    {
        try
        {
            var json = await _storage.LoadAsync();
            var saved = json is null ? null : JsonSerializer.Deserialize<SavedPlayerState>(json);
            return saved is not null && MakesSense(saved) ? saved : null;
        }
        catch (Exception)
        {
            // Storage turned off, or something unreadable in it: the Member starts fresh.
            return null;
        }
    }

    private static bool MakesSense(SavedPlayerState saved) =>
        saved.Queue is not null
        && saved.CurrentIndex < saved.Queue.Count
        && saved.CurrentIndex >= (saved.Queue.Count == 0 ? -1 : 0)
        && saved.Volume is >= 0 and <= 1;

    private async Task<HashSet<Guid>> AvailableAsync(List<PlayerTrack> queue)
    {
        var ids = queue.Where(track => track.IsAvailable).Select(track => track.Id).Distinct().ToList();
        var links = await Task.WhenAll(ids.Select(_links.ForTrackAsync));
        return ids.Where((_, index) => links[index] is not null).ToHashSet();
    }

    private void Save() => _ = SaveAsync();

    private async Task SaveAsync()
    {
        _savedPosition = Position;
        var saved = new SavedPlayerState(_queue, CurrentIndex, Position, Duration, Volume, IsMuted);
        try
        {
            await _storage.SaveAsync(JsonSerializer.Serialize(saved));
        }
        catch (Exception)
        {
            // Storage full or turned off: the music matters more than surviving a reload.
        }
    }

    private void Refuse(PlayerTrack track)
    {
        Notice = $"{track.Title} can't be played right now.";
        Changed?.Invoke();
    }
}
