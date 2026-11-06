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

    [Theory]
    [InlineData(MediaAction.Next)]
    [InlineData(MediaAction.Previous)]
    public async Task Next_and_Previous_leave_the_Track_playing_while_there_is_no_Queue(MediaAction action)
    {
        await _player.PlayAsync(Track);

        await _audio.PressAsync(new MediaCommand(action));

        Assert.Equal(Track, _player.Current);
        Assert.True(_player.IsPlaying);
        Assert.Equal(Link.ToString(), _audio.PlayingSource);
    }

    private sealed class FakePlayLinks : IPlayLinkSource
    {
        private readonly Dictionary<Guid, PlayLink> _links = [];

        public void Add(Guid trackId, Uri link, CoverArt? cover = null) =>
            _links[trackId] = new PlayLink(link, DateTimeOffset.UtcNow.AddMinutes(10), cover);

        public Task<PlayLink?> ForTrackAsync(Guid trackId) => Task.FromResult(_links.GetValueOrDefault(trackId));
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

        public Task PressAsync(MediaCommand command) => MediaCommanded!.Invoke(command);

        public void Report(AudioProgress progress) => Progressed?.Invoke(progress);
    }
}
