using System.Text.Json;
using KleinGeld.Contracts;

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
    private readonly IShuffler _shuffler;
    private readonly IPlayerStorage _storage;

    private List<QueueEntry> _queue = [];

    // Goes up with every press that starts a Track, so the play link of an earlier press that arrives late is dropped.
    private int _attempt;

    // The Track whose play link is on its way, and the Queue it starts: _queue, or the one that will replace it. Queue
    // changes meanwhile must not stop it, and belong around this Track rather than the one still playing.
    private Start? _starting;

    private TimeSpan _savedPosition;

    // After a reload the audio element is empty, and stays so until the Member presses play: a link fetched now could
    // expire long before that.
    private bool _awaitingFirstPlay;

    public PlayerService(IAudioElement audio, IPlayLinkSource links, IShuffler shuffler, IPlayerStorage storage)
    {
        _audio = audio;
        _links = links;
        _shuffler = shuffler;
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

    /// <summary>How loud the player is, from 0 to 1. Muting leaves it as it is.</summary>
    public double Volume { get; private set; } = 1;

    public bool IsMuted { get; private set; }

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

        Shuffle = shuffle;
        foreach (var queue in QueuesInPlay)
        {
            // Never back past the current Track: during Previous, the one being fetched comes before it.
            var last = queue == _queue ? Math.Max(LastPlayed(queue), CurrentIndex) : LastPlayed(queue);
            Reorder(queue, last + 1);
        }

        Save();
        Changed?.Invoke();
    }

    private void Reorder(List<QueueEntry> queue, int from)
    {
        var places = Enumerable.Range(from, queue.Count - from)
            .Where(place => queue[place].SourceOrder is not null)
            .ToList();
        var upcoming = places.Select(place => queue[place]).ToArray();
        if (Shuffle)
        {
            _shuffler.Shuffle(upcoming);
        }
        else
        {
            Array.Sort(upcoming, (a, b) => a.SourceOrder!.Value.CompareTo(b.SourceOrder!.Value));
        }

        foreach (var (place, entry) in places.Zip(upcoming))
        {
            queue[place] = entry;
        }
    }

    public void SetRepeat(RepeatMode repeat)
    {
        Repeat = repeat;
        Save();
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

        if (Current is null && _starting is null)
        {
            return PlayAsync(track);
        }

        var entry = new QueueEntry(track, SourceOrder: null);
        foreach (var queue in QueuesInPlay)
        {
            var place = LastPlayed(queue) + 1;
            queue.Insert(place, entry);
            if (queue == _queue && place <= CurrentIndex)
            {
                CurrentIndex++;
            }
        }

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

        if (Current is null && _starting is null)
        {
            return PlayAsync(track);
        }

        var entry = new QueueEntry(track, SourceOrder: null);
        foreach (var queue in QueuesInPlay)
        {
            queue.Add(entry);
        }

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

        // Set directly: the saved Queue is already in its shuffled order, and SetShuffle would shuffle it again.
        Shuffle = saved.Shuffle;
        Repeat = saved.Repeat;

        var available = await AvailableAsync(saved.Queue);
        if (attempt == _attempt)
        {
            var isCurrentKept = saved.CurrentIndex >= 0 && available.Contains(saved.Queue[saved.CurrentIndex].Track.Id);
            _queue = saved.Queue.Where(entry => available.Contains(entry.Track.Id)).ToList();
            var keptBefore = saved.Queue.Take(saved.CurrentIndex).Count(entry => available.Contains(entry.Track.Id));
            CurrentIndex = Math.Min(keptBefore, _queue.Count - 1);
            Position = _savedPosition = isCurrentKept ? saved.Position : TimeSpan.Zero;
            Duration = isCurrentKept ? saved.Duration : TimeSpan.Zero;
            _awaitingFirstPlay = Current is not null;
        }

        Changed?.Invoke();
    }

    // Only a Track ending by itself loops: Next always moves on. It starts over, because its play link may have expired.
    private Task EndAsync() =>
        Repeat == RepeatMode.One && Current is not null ? StartAsync(_queue, CurrentIndex) : NextAsync();

    private async Task StartAsync(List<QueueEntry> queue, int index)
    {
        var attempt = ++_attempt;
        var entry = queue[index];
        var track = entry.Track;
        _starting = new Start(queue, entry);
        var link = await LinkOrNothingAsync(track.Id);
        if (attempt != _attempt)
        {
            return;
        }

        _starting = null;

        // By reference, and looked up again: the Queue may have changed, and the same Track can be in it twice.
        var place = queue.FindIndex(candidate => ReferenceEquals(candidate, entry));
        if (place < 0)
        {
            // The Member removed it from the Queue while its link was on the way.
            await NextAsync();
            return;
        }

        if (link is null)
        {
            Refuse(track);
            return;
        }

        _queue = queue;
        CurrentIndex = place;
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

    // The Queues a change must reach: the one a start is about to put in place, and the one that stays if it's refused.
    private IEnumerable<List<QueueEntry>> QueuesInPlay =>
        _starting is { } starting && starting.Queue != _queue
            ? Current is null ? [starting.Queue] : [starting.Queue, _queue]
            : [_queue];

    // Where the Tracks still to come begin, less one: after the Track being started, or else the current one.
    private int LastPlayed(List<QueueEntry> queue)
    {
        var starting = _starting is { } start && start.Queue == queue
            ? queue.FindIndex(candidate => ReferenceEquals(candidate, start.Entry))
            : -1;
        return starting >= 0 ? starting : CurrentIndex;
    }

    private async Task PlayRestoredAsync()
    {
        var attempt = ++_attempt;
        _starting = null;
        var track = Current!;
        var link = await LinkOrNothingAsync(track.Id);
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
        && saved.Queue.All(entry => entry?.Track is not null)
        && Enum.IsDefined(saved.Repeat)
        && saved.CurrentIndex < saved.Queue.Count
        && saved.CurrentIndex >= (saved.Queue.Count == 0 ? -1 : 0)
        && saved.Volume is >= 0 and <= 1;

    private async Task<HashSet<Guid>> AvailableAsync(List<QueueEntry> queue)
    {
        var ids = queue.Where(entry => entry.Track.IsAvailable).Select(entry => entry.Track.Id).Distinct().ToList();
        var kept = await Task.WhenAll(ids.Select(IsNotRefusedAsync));
        return ids.Where((_, index) => kept[index]).ToHashSet();
    }

    // Only a refusal drops a Track: a bad moment for the server must not cost the Member their Queue.
    private async Task<bool> IsNotRefusedAsync(Guid trackId)
    {
        try
        {
            return await _links.ForTrackAsync(trackId) is not null;
        }
        catch (PlayLinkFailedException)
        {
            return true;
        }
    }

    private async Task<PlayLink?> LinkOrNothingAsync(Guid trackId)
    {
        try
        {
            return await _links.ForTrackAsync(trackId);
        }
        catch (PlayLinkFailedException)
        {
            return null;
        }
    }

    private void Save() => _ = SaveAsync();

    private async Task SaveAsync()
    {
        _savedPosition = Position;
        var saved = new SavedPlayerState(_queue, CurrentIndex, Position, Duration, Volume, IsMuted, Shuffle, Repeat);
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

    private sealed record Start(List<QueueEntry> Queue, QueueEntry Entry);
}
