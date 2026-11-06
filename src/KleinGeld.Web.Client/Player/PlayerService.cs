namespace KleinGeld.Web.Client.Player;

/// <summary>
/// Holds the playback state for this browser tab and raises <see cref="Changed"/> whenever it changes.
/// </summary>
public sealed class PlayerService
{
    private static readonly TimeSpan RestartAfter = TimeSpan.FromSeconds(3);

    private readonly IAudioElement _audio;
    private readonly IPlayLinkSource _links;
    private readonly IShuffler _shuffler;

    private List<QueueEntry> _queue = [];

    // Goes up with every press that starts a Track or shifts the Queue, so a play link that arrives late is dropped.
    private int _attempt;

    public PlayerService(IAudioElement audio, IPlayLinkSource links, IShuffler shuffler)
    {
        _audio = audio;
        _links = links;
        _shuffler = shuffler;
        _audio.Progressed += progress =>
        {
            Position = progress.Position;
            Duration = progress.Duration;
            IsPlaying = progress.IsPlaying;
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
        _audio.Ended += EndAsync;
    }

    public event Action? Changed;

    /// <summary>The whole Queue: the Tracks already played, the current one and those still to come.</summary>
    public IReadOnlyList<PlayerTrack> Queue => _queue.Select(entry => entry.Track).ToList();

    /// <summary>Where <see cref="Current"/> is in the <see cref="Queue"/>, or -1 before anything has played.</summary>
    public int CurrentIndex { get; private set; } = -1;

    public PlayerTrack? Current => CurrentIndex >= 0 ? _queue[CurrentIndex].Track : null;

    public bool IsPlaying { get; private set; }

    public TimeSpan Position { get; private set; }

    public TimeSpan Duration { get; private set; }

    public bool Shuffle { get; private set; }

    public RepeatMode Repeat { get; private set; }

    /// <summary>A short message for the Member about what just went wrong, if anything did.</summary>
    public string? Notice { get; private set; }

    /// <summary>Whether a phone shows the full player over the page rather than the mini player.</summary>
    public bool IsFullPlayerOpen { get; private set; }

    /// <summary>
    /// Shuffles the Tracks still to come, or puts them back in their original order after the current Track. Tracks
    /// from Play next and Add to queue stay where they are.
    /// </summary>
    public void SetShuffle(bool shuffle)
    {
        if (shuffle == Shuffle)
        {
            return;
        }

        _attempt++;
        Shuffle = shuffle;
        var places = Enumerable.Range(CurrentIndex + 1, _queue.Count - CurrentIndex - 1)
            .Where(place => _queue[place].SourceOrder is not null)
            .ToList();
        var upcoming = places.Select(place => _queue[place]).ToArray();
        if (shuffle)
        {
            _shuffler.Shuffle(upcoming);
        }
        else
        {
            Array.Sort(upcoming, (a, b) => a.SourceOrder!.Value.CompareTo(b.SourceOrder!.Value));
        }

        foreach (var (place, entry) in places.Zip(upcoming))
        {
            _queue[place] = entry;
        }

        Changed?.Invoke();
    }

    public void SetRepeat(RepeatMode repeat)
    {
        Repeat = repeat;
        Changed?.Invoke();
    }

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

        var queue = source.Where(track => track.IsAvailable)
            .Select((track, order) => new QueueEntry(track, order))
            .ToList();
        var pressed = source.Take(start).Count(track => track.IsAvailable);
        if (!Shuffle)
        {
            return StartAsync(queue, pressed);
        }

        var rest = queue.Where((_, index) => index != pressed).ToArray();
        _shuffler.Shuffle(rest);
        return StartAsync([queue[pressed], .. rest], 0);
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
        _queue.Insert(CurrentIndex + 1, new QueueEntry(track, SourceOrder: null));
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

        _queue.Add(new QueueEntry(track, SourceOrder: null));
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

    /// <summary>Moves on to the next Track. After the last one it wraps with repeat all and otherwise stops there.</summary>
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
        else if (Repeat == RepeatMode.All)
        {
            await StartAsync(_queue, 0);
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
        await _audio.SeekAsync(position);
        Position = position;
        Changed?.Invoke();
    }

    // Only a Track ending by itself loops: Next always moves on. It starts over, because its play link may have expired.
    private Task EndAsync() =>
        Repeat == RepeatMode.One && Current is not null ? StartAsync(_queue, CurrentIndex) : NextAsync();

    private async Task StartAsync(List<QueueEntry> queue, int index)
    {
        var attempt = ++_attempt;
        var track = queue[index].Track;
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
        Notice = null;
        Position = TimeSpan.Zero;
        Duration = TimeSpan.Zero;
        IsPlaying = true;
        await _audio.ShowMetadataAsync(new MediaMetadata(track.Title, track.Artist, link.Cover));
        await _audio.PlayAsync(link.Url.ToString());
        Changed?.Invoke();
    }

    private void Refuse(PlayerTrack track)
    {
        Notice = $"{track.Title} can't be played right now.";
        Changed?.Invoke();
    }

    /// <summary>
    /// A place in the Queue. <paramref name="SourceOrder"/> is where the Track was in the list Play was pressed on, and
    /// null for one added by Play next or Add to queue, which shuffle leaves alone.
    /// </summary>
    private sealed record QueueEntry(PlayerTrack Track, int? SourceOrder);
}
