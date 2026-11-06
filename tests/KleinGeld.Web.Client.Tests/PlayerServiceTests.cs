using KleinGeld.Contracts;
using KleinGeld.Web.Client.Player;

namespace KleinGeld.Web.Client.Tests;

public sealed class PlayerServiceTests
{
    private static readonly PlayerTrack Track = new(Guid.CreateVersion7(), "Kapél", "De Kleingeld");
    private static readonly Uri Link = new("https://blobs.example/track.m4a?sig=abc");

    private readonly FakeAudioElement _audio = new();
    private readonly FakePlayLinks _links = new();
    private readonly PlayerService _player;

    public PlayerServiceTests()
    {
        _links.Add(Track.Id, Link);
        _player = new PlayerService(_audio, _links);
    }

    [Fact]
    public async Task Playing_a_Track_starts_it_from_a_fresh_play_link_and_reports_playing()
    {
        var changes = 0;
        _player.Changed += () => changes++;

        await _player.PlayAsync(Track);

        Assert.Equal(Link.ToString(), _audio.PlayingSource);
        Assert.Equal(Track, _player.Current);
        Assert.True(_player.IsPlaying);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task The_mini_player_opens_the_full_player_and_the_music_plays_on_when_it_closes()
    {
        await _player.PlayAsync(Track);
        Assert.False(_player.IsFullPlayerOpen);
        var changes = 0;
        _player.Changed += () => changes++;

        _player.OpenFullPlayer();
        Assert.True(_player.IsFullPlayerOpen);

        _player.CloseFullPlayer();
        Assert.False(_player.IsFullPlayerOpen);
        Assert.True(_player.IsPlaying);
        Assert.Equal(Track, _player.Current);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task Pausing_pauses_the_audio_and_keeps_the_Track()
    {
        await _player.PlayAsync(Track);
        var changes = 0;
        _player.Changed += () => changes++;

        await _player.PauseAsync();

        Assert.True(_audio.IsPaused);
        Assert.False(_player.IsPlaying);
        Assert.Equal(Track, _player.Current);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task Resuming_carries_on_from_the_same_audio_without_a_new_link()
    {
        await _player.PlayAsync(Track);
        await _player.PauseAsync();
        _links.Add(Track.Id, new Uri("https://blobs.example/other"));

        await _player.ResumeAsync();

        Assert.False(_audio.IsPaused);
        Assert.Equal(Link.ToString(), _audio.PlayingSource);
        Assert.True(_player.IsPlaying);
    }

    [Fact]
    public async Task Seeking_moves_the_audio_and_the_reported_position()
    {
        await _player.PlayAsync(Track);

        await _player.SeekAsync(TimeSpan.FromSeconds(42));

        Assert.Equal(TimeSpan.FromSeconds(42), _audio.SoughtTo);
        Assert.Equal(TimeSpan.FromSeconds(42), _player.Position);
    }

    [Fact]
    public async Task The_audio_reports_its_progress_and_when_it_stops()
    {
        await _player.PlayAsync(Track);
        var changes = 0;
        _player.Changed += () => changes++;

        _audio.Report(new AudioProgress(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(180), IsPlaying: false));

        Assert.Equal(TimeSpan.FromSeconds(3), _player.Position);
        Assert.Equal(TimeSpan.FromSeconds(180), _player.Duration);
        Assert.False(_player.IsPlaying);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task A_refused_play_link_leaves_the_player_as_it_was_with_a_notice()
    {
        var refused = new PlayerTrack(Guid.CreateVersion7(), "Weg", "De Kleingeld");
        await _player.PlayAsync(Track);

        await _player.PlayAsync(refused);

        Assert.Equal(Track, _player.Current);
        Assert.Equal(Link.ToString(), _audio.PlayingSource);
        Assert.Equal("Weg can't be played right now.", _player.Notice);

        await _player.PlayAsync(Track);
        Assert.Null(_player.Notice);
    }

    [Fact]
    public async Task Every_Track_change_shows_its_title_Artist_and_cover_on_the_lock_screen()
    {
        var cover = new CoverArt(new Uri("https://blobs.example/300.jpg"), new Uri("https://blobs.example/1000.jpg"));
        var next = new PlayerTrack(Guid.CreateVersion7(), "Weg", "Oom Piet");
        _links.Add(next.Id, Link, cover);

        await _player.PlayAsync(Track);
        await _player.PlayAsync(next);

        Assert.Equal(
            [new MediaMetadata("Kapél", "De Kleingeld", null), new MediaMetadata("Weg", "Oom Piet", cover)],
            _audio.Metadata);
    }

    [Fact]
    public async Task A_refused_play_link_leaves_the_lock_screen_on_the_playing_Track()
    {
        await _player.PlayAsync(Track);

        await _player.PlayAsync(new PlayerTrack(Guid.CreateVersion7(), "Weg", "Oom Piet"));

        Assert.Equal([new MediaMetadata("Kapél", "De Kleingeld", null)], _audio.Metadata);
    }

    [Fact]
    public async Task The_media_keys_pause_and_resume_the_Track()
    {
        await _player.PlayAsync(Track);

        await _audio.PressAsync(new MediaCommand(MediaAction.Pause));
        Assert.True(_audio.IsPaused);
        Assert.False(_player.IsPlaying);

        await _audio.PressAsync(new MediaCommand(MediaAction.Play));
        Assert.False(_audio.IsPaused);
        Assert.True(_player.IsPlaying);
    }

    [Fact]
    public async Task Seeking_from_the_lock_screen_moves_the_audio()
    {
        await _player.PlayAsync(Track);

        await _audio.PressAsync(new MediaCommand(MediaAction.Seek, TimeSpan.FromSeconds(42)));

        Assert.Equal(TimeSpan.FromSeconds(42), _audio.SoughtTo);
        Assert.Equal(TimeSpan.FromSeconds(42), _player.Position);
    }

    [Fact]
    public async Task Play_replaces_the_whole_Queue_with_the_source_list_and_starts_at_the_pressed_Track()
    {
        var album = Album("Een", "Twee", "Drie");
        await _player.PlayAsync(Album("Oud"), 0);

        await _player.PlayAsync(album, 1);

        Assert.Equal(album, _player.Queue);
        Assert.Equal(album[1], _player.Current);
        Assert.Equal(LinkOf(album[1]), _audio.PlayingSource);
    }

    [Fact]
    public async Task Play_leaves_Unavailable_Tracks_out_of_the_Queue()
    {
        var album = Album("Een", "Twee", "Drie", "Vier");
        album[0] = album[0] with { IsAvailable = false };
        album[2] = album[2] with { IsAvailable = false };

        await _player.PlayAsync(album, 3);

        Assert.Equal([album[1], album[3]], _player.Queue);
        Assert.Equal(album[3], _player.Current);
    }

    [Fact]
    public async Task Pressing_an_Unavailable_Track_keeps_the_Queue_and_gives_a_notice()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 0);
        var other = Album("Weg");
        other[0] = other[0] with { IsAvailable = false };

        await _player.PlayAsync(other, 0);

        Assert.Equal(album, _player.Queue);
        Assert.Equal(album[0], _player.Current);
        Assert.Equal("Weg can't be played right now.", _player.Notice);
    }

    [Fact]
    public async Task A_refused_play_link_on_Play_keeps_the_old_Queue()
    {
        var album = Album("Een");
        await _player.PlayAsync(album, 0);

        await _player.PlayAsync([new PlayerTrack(Guid.CreateVersion7(), "Weg", "Oom Piet")], 0);

        Assert.Equal(album, _player.Queue);
    }

    [Fact]
    public async Task The_last_press_wins_when_an_earlier_play_link_arrives_late()
    {
        var album = Album("Een", "Twee");
        var slow = _links.Hold(album[0].Id);
        var first = _player.PlayAsync(album, 0);

        await _player.PlayAsync(album, 1);
        slow.SetResult();
        await first;

        Assert.Equal(album[1], _player.Current);
        Assert.Equal(LinkOf(album[1]), _audio.PlayingSource);
    }

    [Fact]
    public async Task Next_plays_the_following_Track_from_a_fresh_play_link()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 0);

        await _player.NextAsync();

        Assert.Equal(album[1], _player.Current);
        Assert.Equal(LinkOf(album[1]), _audio.PlayingSource);
        Assert.True(_player.IsPlaying);
        Assert.Equal(TimeSpan.Zero, _player.Position);
    }

    [Fact]
    public async Task A_Track_ending_by_itself_moves_on_to_the_next()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 0);

        await _audio.EndAsync();

        Assert.Equal(album[1], _player.Current);
        Assert.True(_player.IsPlaying);
    }

    [Fact]
    public async Task With_repeat_off_the_player_stops_on_the_last_Track_when_it_ends()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 1);
        var source = _audio.PlayingSource;
        _audio.Report(new AudioProgress(TimeSpan.FromSeconds(180), TimeSpan.FromSeconds(180), IsPlaying: false));

        await _audio.EndAsync();

        Assert.Equal(album[1], _player.Current);
        Assert.False(_player.IsPlaying);
        Assert.Equal(source, _audio.PlayingSource);
    }

    [Fact]
    public async Task Next_on_the_last_Track_stops_there()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 1);

        await _player.NextAsync();

        Assert.Equal(album[1], _player.Current);
        Assert.False(_player.IsPlaying);
        Assert.True(_audio.IsPaused);
    }

    [Fact]
    public async Task A_refused_play_link_on_Next_stays_on_the_Track_with_a_notice()
    {
        var album = Album("Een");
        album.Add(new PlayerTrack(Guid.CreateVersion7(), "Weg", "Oom Piet"));
        await _player.PlayAsync(album, 0);

        await _player.NextAsync();

        Assert.Equal(album[0], _player.Current);
        Assert.Equal("Weg can't be played right now.", _player.Notice);
    }

    [Fact]
    public async Task Previous_more_than_3_seconds_in_restarts_the_Track()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 1);
        _audio.Report(new AudioProgress(TimeSpan.FromSeconds(3.5), TimeSpan.FromSeconds(180), IsPlaying: true));

        await _player.PreviousAsync();

        Assert.Equal(album[1], _player.Current);
        Assert.Equal(TimeSpan.Zero, _audio.SoughtTo);
        Assert.Equal(TimeSpan.Zero, _player.Position);
    }

    [Fact]
    public async Task Previous_at_exactly_3_seconds_goes_back_one()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 1);
        _audio.Report(new AudioProgress(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(180), IsPlaying: true));

        await _player.PreviousAsync();

        Assert.Equal(album[0], _player.Current);
        Assert.Equal(LinkOf(album[0]), _audio.PlayingSource);
        Assert.Null(_audio.SoughtTo);
    }

    [Fact]
    public async Task Previous_on_the_first_Track_restarts_it()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 0);

        await _player.PreviousAsync();

        Assert.Equal(album[0], _player.Current);
        Assert.Equal(TimeSpan.Zero, _audio.SoughtTo);
    }

    [Fact]
    public async Task Next_and_Previous_do_nothing_before_anything_has_played()
    {
        await _player.NextAsync();
        await _player.PreviousAsync();

        Assert.Null(_player.Current);
        Assert.Null(_audio.SoughtTo);
        Assert.False(_audio.IsPaused);
    }

    [Fact]
    public async Task The_media_keys_move_through_the_Queue()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 0);

        await _audio.PressAsync(new MediaCommand(MediaAction.Next));
        Assert.Equal(album[1], _player.Current);
        Assert.Equal(LinkOf(album[1]), _audio.PlayingSource);

        await _audio.PressAsync(new MediaCommand(MediaAction.Previous));
        Assert.Equal(album[0], _player.Current);
        Assert.Equal(LinkOf(album[0]), _audio.PlayingSource);
    }

    [Fact]
    public async Task Play_next_inserts_after_the_current_Track_and_leaves_it_playing()
    {
        var album = Album("Een", "Twee", "Drie");
        var extra = Album("Extra")[0];
        await _player.PlayAsync(album, 1);

        await _player.PlayNextAsync(extra);

        Assert.Equal([album[0], album[1], extra, album[2]], _player.Queue);
        Assert.Equal(album[1], _player.Current);
        await _player.NextAsync();
        Assert.Equal(extra, _player.Current);
    }

    [Fact]
    public async Task Add_to_queue_appends_and_leaves_the_current_Track_playing()
    {
        var album = Album("Een", "Twee");
        var extra = Album("Extra")[0];
        await _player.PlayAsync(album, 0);

        await _player.AddToQueueAsync(extra);

        Assert.Equal([album[0], album[1], extra], _player.Queue);
        Assert.Equal(album[0], _player.Current);
    }

    [Fact]
    public async Task The_same_Track_can_be_in_the_Queue_twice()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 0);
        await _player.AddToQueueAsync(album[0]);

        await _player.NextAsync();
        await _player.NextAsync();

        Assert.Equal(2, _player.CurrentIndex);
        Assert.Equal(album[0], _player.Current);
    }

    [Fact]
    public async Task With_nothing_playing_Play_next_and_Add_to_queue_start_the_Track()
    {
        var first = Album("Een")[0];

        await _player.AddToQueueAsync(first);

        Assert.Equal(first, _player.Current);
        Assert.Equal([first], _player.Queue);
        Assert.True(_player.IsPlaying);
    }

    [Fact]
    public async Task Unavailable_Tracks_cant_be_added_to_the_Queue()
    {
        var album = Album("Een");
        var gone = Album("Weg")[0] with { IsAvailable = false };
        await _player.PlayAsync(album, 0);

        await _player.PlayNextAsync(gone);
        await _player.AddToQueueAsync(gone);

        Assert.Equal(album, _player.Queue);
    }

    [Fact]
    public async Task Removing_an_upcoming_Track_takes_it_out_of_the_Queue()
    {
        var album = Album("Een", "Twee", "Drie");
        await _player.PlayAsync(album, 0);
        var changes = 0;
        _player.Changed += () => changes++;

        _player.RemoveFromQueue(1);

        Assert.Equal([album[0], album[2]], _player.Queue);
        Assert.Equal(album[0], _player.Current);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task Removing_an_earlier_Track_keeps_the_current_one()
    {
        var album = Album("Een", "Twee", "Drie");
        await _player.PlayAsync(album, 2);

        _player.RemoveFromQueue(0);

        Assert.Equal(album[2], _player.Current);
        await _player.PreviousAsync();
        Assert.Equal(album[1], _player.Current);
    }

    [Fact]
    public async Task The_current_Track_cant_be_removed()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 0);

        _player.RemoveFromQueue(0);

        Assert.Equal(album, _player.Queue);
    }

    [Fact]
    public async Task Repeat_all_wraps_to_the_start_when_the_last_Track_ends()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 1);
        _player.SetRepeat(RepeatMode.All);

        await _audio.EndAsync();

        Assert.Equal(album[0], _player.Current);
        Assert.Equal(LinkOf(album[0]), _audio.PlayingSource);
        Assert.True(_player.IsPlaying);
    }

    [Fact]
    public async Task Repeat_all_wraps_to_the_start_on_Next_from_the_last_Track()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 1);
        _player.SetRepeat(RepeatMode.All);

        await _player.NextAsync();

        Assert.Equal(album[0], _player.Current);
        Assert.True(_player.IsPlaying);
    }

    [Fact]
    public async Task Repeat_one_plays_the_Track_again_from_a_fresh_play_link_when_it_ends_by_itself()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 0);
        _player.SetRepeat(RepeatMode.One);
        var fresh = new Uri("https://blobs.example/fresh.m4a");
        _links.Add(album[0].Id, fresh);
        _audio.Report(new AudioProgress(TimeSpan.FromSeconds(180), TimeSpan.FromSeconds(180), IsPlaying: false));

        await _audio.EndAsync();

        Assert.Equal(album[0], _player.Current);
        Assert.Equal(fresh.ToString(), _audio.PlayingSource);
        Assert.True(_player.IsPlaying);
        Assert.Equal(TimeSpan.Zero, _player.Position);
    }

    [Fact]
    public async Task Repeat_one_loops_the_last_Track_when_it_ends_by_itself()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 1);
        _player.SetRepeat(RepeatMode.One);
        _audio.Report(new AudioProgress(TimeSpan.FromSeconds(180), TimeSpan.FromSeconds(180), IsPlaying: false));

        await _audio.EndAsync();

        Assert.Equal(album[1], _player.Current);
        Assert.True(_player.IsPlaying);
    }

    [Fact]
    public async Task With_repeat_one_Next_still_moves_on()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 0);
        _player.SetRepeat(RepeatMode.One);

        await _player.NextAsync();

        Assert.Equal(album[1], _player.Current);
    }

    [Fact]
    public async Task With_repeat_one_the_Next_media_key_still_moves_on()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 0);
        _player.SetRepeat(RepeatMode.One);

        await _audio.PressAsync(new MediaCommand(MediaAction.Next));

        Assert.Equal(album[1], _player.Current);
    }

    [Fact]
    public async Task With_repeat_one_Next_on_the_last_Track_stops_there()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 1);
        _player.SetRepeat(RepeatMode.One);

        await _player.NextAsync();

        Assert.Equal(album[1], _player.Current);
        Assert.False(_player.IsPlaying);
    }

    [Fact]
    public async Task Choosing_repeat_tells_the_player_bar()
    {
        var changes = 0;
        _player.Changed += () => changes++;

        _player.SetRepeat(RepeatMode.All);

        Assert.Equal(RepeatMode.All, _player.Repeat);
        Assert.Equal(1, changes);
    }

    private List<PlayerTrack> Album(params string[] titles)
    {
        var album = titles.Select(title => new PlayerTrack(Guid.CreateVersion7(), title, "De Kleingeld")).ToList();
        foreach (var track in album)
        {
            _links.Add(track.Id, new Uri(LinkOf(track)));
        }

        return album;
    }

    private static string LinkOf(PlayerTrack track) => $"https://blobs.example/{track.Id}.m4a";

    private sealed class FakePlayLinks : IPlayLinkSource
    {
        private readonly Dictionary<Guid, PlayLink> _links = [];
        private readonly Dictionary<Guid, TaskCompletionSource> _held = [];

        public void Add(Guid trackId, Uri link, CoverArt? cover = null) =>
            _links[trackId] = new PlayLink(link, DateTimeOffset.UtcNow.AddMinutes(10), cover);

        /// <summary>Keeps the Track's link back until the returned source is completed.</summary>
        public TaskCompletionSource Hold(Guid trackId) => _held[trackId] = new TaskCompletionSource();

        public async Task<PlayLink?> ForTrackAsync(Guid trackId)
        {
            if (_held.TryGetValue(trackId, out var held))
            {
                await held.Task;
            }

            return _links.GetValueOrDefault(trackId);
        }
    }

    private sealed class FakeAudioElement : IAudioElement
    {
        public List<MediaMetadata> Metadata { get; } = [];

        public string? PlayingSource { get; private set; }

        public bool IsPaused { get; private set; }

        public Task PlayAsync(string source)
        {
            PlayingSource = source;
            IsPaused = false;
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            IsPaused = true;
            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            IsPaused = false;
            return Task.CompletedTask;
        }

        public Task SeekAsync(TimeSpan position)
        {
            SoughtTo = position;
            return Task.CompletedTask;
        }

        public Task ShowMetadataAsync(MediaMetadata metadata)
        {
            Metadata.Add(metadata);
            return Task.CompletedTask;
        }

        public TimeSpan? SoughtTo { get; private set; }

        public event Action<AudioProgress>? Progressed;

        public event Func<MediaCommand, Task>? MediaCommanded;

        public event Func<Task>? Ended;

        public Task EndAsync() => Ended!.Invoke();

        public Task PressAsync(MediaCommand command) => MediaCommanded!.Invoke(command);

        public void Report(AudioProgress progress) => Progressed?.Invoke(progress);
    }
}
