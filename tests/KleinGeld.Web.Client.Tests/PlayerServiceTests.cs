using KleinGeld.Contracts;
using KleinGeld.Web.Client.Player;

namespace KleinGeld.Web.Client.Tests;

public sealed class PlayerServiceTests
{
    private static readonly PlayerTrack Track = new(Guid.CreateVersion7(), "Kapél", "De Kleingeld");
    private static readonly Uri Link = new("https://blobs.example/track.m4a?sig=abc");

    private readonly FakeAudioElement _audio = new();
    private readonly FakePlayLinks _links = new();
    private readonly FakePlayerStorage _storage = new();
    private readonly PlayerService _player;

    public PlayerServiceTests()
    {
        _links.Add(Track.Id, Link);
        _player = new PlayerService(_audio, _links, new RotatingShuffler(), _storage);
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

    [Fact]
    public async Task With_shuffle_on_Play_starts_with_the_pressed_Track_and_the_rest_random()
    {
        var album = Album("Een", "Twee", "Drie", "Vier", "Vijf");
        _player.SetShuffle(true);

        await _player.PlayAsync(album, 2);

        Assert.Equal(["Drie", "Twee", "Vier", "Vijf", "Een"], Titles());
        Assert.Equal(album[2], _player.Current);
        Assert.Equal(LinkOf(album[2]), _audio.PlayingSource);
    }

    [Fact]
    public async Task With_shuffle_on_Play_still_leaves_Unavailable_Tracks_out()
    {
        var album = Album("Een", "Twee", "Drie");
        album[0] = album[0] with { IsAvailable = false };
        _player.SetShuffle(true);

        await _player.PlayAsync(album, 1);

        Assert.Equal(["Twee", "Drie"], Titles());
    }

    [Fact]
    public async Task Turning_shuffle_on_mid_Queue_shuffles_only_the_upcoming_Tracks()
    {
        var album = Album("Een", "Twee", "Drie", "Vier", "Vijf");
        await _player.PlayAsync(album, 1);

        _player.SetShuffle(true);

        Assert.Equal(["Een", "Twee", "Vier", "Vijf", "Drie"], Titles());
        Assert.Equal(album[1], _player.Current);
    }

    [Fact]
    public async Task Turning_shuffle_on_leaves_the_current_Track_playing()
    {
        var album = Album("Een", "Twee", "Drie");
        await _player.PlayAsync(album, 0);
        var changes = 0;
        _player.Changed += () => changes++;

        _player.SetShuffle(true);

        Assert.True(_player.Shuffle);
        Assert.Equal(LinkOf(album[0]), _audio.PlayingSource);
        Assert.Single(_audio.Metadata);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task Turning_shuffle_on_twice_shuffles_once()
    {
        await _player.PlayAsync(Album("Een", "Twee", "Drie"), 0);

        _player.SetShuffle(true);
        _player.SetShuffle(true);

        Assert.Equal(["Een", "Drie", "Twee"], Titles());
    }

    [Fact]
    public async Task Turning_shuffle_off_restores_the_original_order_of_the_upcoming_Tracks()
    {
        await _player.PlayAsync(Album("Een", "Twee", "Drie", "Vier", "Vijf"), 0);
        _player.SetShuffle(true);
        await _player.NextAsync();
        Assert.Equal(["Een", "Drie", "Vier", "Vijf", "Twee"], Titles());

        _player.SetShuffle(false);

        Assert.Equal(["Een", "Drie", "Twee", "Vier", "Vijf"], Titles());
        Assert.Equal("Drie", _player.Current?.Title);
    }

    [Fact]
    public async Task Turning_shuffle_off_puts_every_upcoming_Track_after_the_pressed_one()
    {
        _player.SetShuffle(true);
        await _player.PlayAsync(Album("Een", "Twee", "Drie", "Vier"), 2);

        _player.SetShuffle(false);

        Assert.Equal(["Drie", "Een", "Twee", "Vier"], Titles());
        Assert.Equal(0, _player.CurrentIndex);
    }

    [Fact]
    public async Task Turning_shuffle_off_tells_apart_a_Track_that_is_in_the_list_twice()
    {
        var album = Album("Een", "Twee", "Drie");
        await _player.PlayAsync([album[0], album[1], album[2], album[1], album[0]], 0);
        _player.SetShuffle(true);
        Assert.Equal(["Een", "Drie", "Twee", "Een", "Twee"], Titles());

        _player.SetShuffle(false);

        Assert.Equal(["Een", "Twee", "Drie", "Twee", "Een"], Titles());
    }

    [Fact]
    public async Task A_Play_next_Track_keeps_its_place_when_shuffle_turns_on_and_off()
    {
        await _player.PlayAsync(Album("Een", "Twee", "Drie", "Vier"), 0);
        await _player.PlayNextAsync(Album("Extra")[0]);

        _player.SetShuffle(true);
        Assert.Equal(["Een", "Extra", "Drie", "Vier", "Twee"], Titles());

        _player.SetShuffle(false);
        Assert.Equal(["Een", "Extra", "Twee", "Drie", "Vier"], Titles());
    }

    [Fact]
    public async Task An_Add_to_queue_Track_keeps_its_place_when_shuffle_turns_on_and_off()
    {
        await _player.PlayAsync(Album("Een", "Twee", "Drie", "Vier"), 0);
        await _player.AddToQueueAsync(Album("Extra")[0]);

        _player.SetShuffle(true);
        Assert.Equal(["Een", "Drie", "Vier", "Twee", "Extra"], Titles());

        _player.SetShuffle(false);
        Assert.Equal(["Een", "Twee", "Drie", "Vier", "Extra"], Titles());
    }

    [Fact]
    public async Task Tracks_added_while_shuffled_keep_their_place_when_shuffle_turns_off()
    {
        await _player.PlayAsync(Album("Een", "Twee", "Drie", "Vier"), 0);
        _player.SetShuffle(true);
        await _player.PlayNextAsync(Album("Eerst")[0]);
        await _player.AddToQueueAsync(Album("Laatst")[0]);
        Assert.Equal(["Een", "Eerst", "Drie", "Vier", "Twee", "Laatst"], Titles());

        _player.SetShuffle(false);

        Assert.Equal(["Een", "Eerst", "Twee", "Drie", "Vier", "Laatst"], Titles());
    }

    [Fact]
    public async Task A_Track_removed_while_shuffled_stays_out_when_shuffle_turns_off()
    {
        await _player.PlayAsync(Album("Een", "Twee", "Drie", "Vier"), 0);
        _player.SetShuffle(true);

        _player.RemoveFromQueue(2);
        _player.SetShuffle(false);

        Assert.Equal(["Een", "Twee", "Drie"], Titles());
    }

    [Fact]
    public async Task Repeat_all_wraps_to_the_start_of_the_shuffled_Queue()
    {
        _player.SetShuffle(true);
        _player.SetRepeat(RepeatMode.All);
        await _player.PlayAsync(Album("Een", "Twee", "Drie"), 1);
        await _player.NextAsync();
        await _player.NextAsync();

        await _player.NextAsync();

        Assert.Equal("Twee", _player.Current?.Title);
        Assert.Equal(["Twee", "Drie", "Een"], Titles());
    }

    [Fact]
    public async Task A_play_link_arriving_after_shuffle_shifted_the_Queue_is_dropped()
    {
        var album = Album("Een", "Twee", "Drie");
        await _player.PlayAsync(album, 0);
        var slow = _links.Hold(album[1].Id);
        var next = _player.NextAsync();

        _player.SetShuffle(true);
        slow.SetResult();
        await next;

        Assert.Equal(album[0], _player.Current);
        Assert.Equal(LinkOf(album[0]), _audio.PlayingSource);
    }

    private List<string> Titles() => _player.Queue.Select(track => track.Title).ToList();

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

    [Fact]
    public async Task Setting_the_volume_changes_the_audio_and_tells_the_page()
    {
        var changes = 0;
        _player.Changed += () => changes++;

        await _player.SetVolumeAsync(0.4);

        Assert.Equal(0.4, _audio.Volume);
        Assert.Equal(0.4, _player.Volume);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task Muting_silences_the_audio_and_unmuting_brings_back_the_same_volume()
    {
        await _player.SetVolumeAsync(0.4);

        await _player.ToggleMuteAsync();
        Assert.True(_audio.IsMuted);
        Assert.True(_player.IsMuted);

        await _player.ToggleMuteAsync();
        Assert.False(_audio.IsMuted);
        Assert.False(_player.IsMuted);
        Assert.Equal(0.4, _player.Volume);
    }

    [Fact]
    public async Task After_a_reload_the_Queue_Track_position_and_volume_are_back_and_nothing_plays()
    {
        var (first, second) = TwoTracks();
        await _player.PlayAsync([first, second], 1);
        await _player.SetVolumeAsync(0.4);
        await _player.ToggleMuteAsync();
        _audio.Report(new AudioProgress(TimeSpan.FromSeconds(42), TimeSpan.FromSeconds(180), true));
        await _player.PauseAsync();

        var (reloaded, audio) = await ReloadAsync();

        Assert.Equal([first, second], reloaded.Queue);
        Assert.Equal(second, reloaded.Current);
        Assert.Equal(TimeSpan.FromSeconds(42), reloaded.Position);
        Assert.Equal(TimeSpan.FromSeconds(180), reloaded.Duration);
        Assert.Equal(0.4, reloaded.Volume);
        Assert.True(reloaded.IsMuted);
        Assert.Equal(0.4, audio.Volume);
        Assert.True(audio.IsMuted);
        Assert.False(reloaded.IsPlaying);
        Assert.Null(audio.PlayingSource);
    }

    [Fact]
    public async Task The_position_is_saved_while_playing_so_a_reload_mid_Track_loses_only_seconds()
    {
        await _player.PlayAsync(Track);
        _audio.Report(new AudioProgress(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(180), true));

        var (reloaded, _) = await ReloadAsync();

        Assert.Equal(TimeSpan.FromSeconds(30), reloaded.Position);
    }

    [Fact]
    public async Task The_audio_ticking_along_does_not_write_to_storage_every_time()
    {
        await _player.PlayAsync(Track);
        var saves = _storage.Saves;

        _audio.Report(new AudioProgress(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(180), true));
        _audio.Report(new AudioProgress(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(180), true));

        Assert.Equal(saves, _storage.Saves);
    }

    [Fact]
    public async Task Pressing_play_after_a_reload_gets_a_fresh_link_and_carries_on_from_the_saved_position()
    {
        await _player.PlayAsync(Track);
        await _player.SeekAsync(TimeSpan.FromSeconds(42));
        var fresh = new Uri("https://blobs.example/fresh");
        _links.Add(Track.Id, fresh);
        var (reloaded, audio) = await ReloadAsync();

        await reloaded.ResumeAsync();

        Assert.Equal(fresh.ToString(), audio.PlayingSource);
        Assert.Equal(TimeSpan.FromSeconds(42), audio.PlayedFrom);
        Assert.True(reloaded.IsPlaying);
        Assert.Equal(Track.Title, Assert.Single(audio.Metadata).Title);
    }

    [Fact]
    public async Task Seeking_after_a_reload_moves_where_play_will_start()
    {
        await _player.PlayAsync(Track);
        var (reloaded, audio) = await ReloadAsync();

        await reloaded.SeekAsync(TimeSpan.FromSeconds(99));
        await reloaded.ResumeAsync();

        Assert.Equal(TimeSpan.FromSeconds(99), audio.PlayedFrom);
    }

    [Fact]
    public async Task Pressing_play_after_a_reload_on_a_refused_Track_says_so_and_stays_paused()
    {
        await _player.PlayAsync(Track);
        var (reloaded, audio) = await ReloadAsync();
        _links.Remove(Track.Id);

        await reloaded.ResumeAsync();

        Assert.False(reloaded.IsPlaying);
        Assert.Null(audio.PlayingSource);
        Assert.Equal("Kapél can't be played right now.", reloaded.Notice);
    }

    [Fact]
    public async Task Changes_to_the_Queue_are_saved()
    {
        var (first, second) = TwoTracks();
        await _player.PlayAsync(first);
        await _player.AddToQueueAsync(second);
        await _player.PlayNextAsync(Track);
        _player.RemoveFromQueue(1);

        var (reloaded, _) = await ReloadAsync();

        Assert.Equal([first, second], reloaded.Queue);
    }

    [Fact]
    public async Task Moving_to_the_next_Track_saves_it_as_the_current_one()
    {
        var (first, second) = TwoTracks();
        await _player.PlayAsync([first, second], 0);
        await _player.NextAsync();

        var (reloaded, _) = await ReloadAsync();

        Assert.Equal(second, reloaded.Current);
        Assert.Equal(TimeSpan.Zero, reloaded.Position);
    }

    [Fact]
    public async Task Restoring_drops_Tracks_that_have_become_unavailable_and_keeps_the_current_one_and_its_position()
    {
        var (first, second) = TwoTracks();
        await _player.PlayAsync([first, second, Track], 1);
        await _player.SeekAsync(TimeSpan.FromSeconds(42));
        _links.Remove(first.Id);

        var (reloaded, _) = await ReloadAsync();

        Assert.Equal([second, Track], reloaded.Queue);
        Assert.Equal(second, reloaded.Current);
        Assert.Equal(TimeSpan.FromSeconds(42), reloaded.Position);
    }

    [Fact]
    public async Task When_the_current_Track_has_become_unavailable_the_next_one_waits_at_its_start()
    {
        var (first, second) = TwoTracks();
        await _player.PlayAsync([first, second, Track], 1);
        await _player.SeekAsync(TimeSpan.FromSeconds(42));
        _links.Remove(second.Id);

        var (reloaded, _) = await ReloadAsync();

        Assert.Equal([first, Track], reloaded.Queue);
        Assert.Equal(Track, reloaded.Current);
        Assert.Equal(TimeSpan.Zero, reloaded.Position);
    }

    [Fact]
    public async Task When_the_last_Track_has_become_unavailable_the_one_before_it_waits_at_its_start()
    {
        var (first, second) = TwoTracks();
        await _player.PlayAsync([first, second], 1);
        _links.Remove(second.Id);

        var (reloaded, _) = await ReloadAsync();

        Assert.Equal(first, reloaded.Current);
    }

    [Fact]
    public async Task When_every_Track_has_become_unavailable_the_Queue_is_empty_but_the_volume_stays()
    {
        await _player.PlayAsync(Track);
        await _player.SetVolumeAsync(0.4);
        _links.Remove(Track.Id);

        var (reloaded, _) = await ReloadAsync();

        Assert.Empty(reloaded.Queue);
        Assert.Null(reloaded.Current);
        Assert.Equal(0.4, reloaded.Volume);
    }

    [Fact]
    public async Task A_failure_to_get_a_play_link_on_reload_keeps_the_Track()
    {
        var (first, second) = TwoTracks();
        await _player.PlayAsync([first, second, Track], 1);
        await _player.SeekAsync(TimeSpan.FromSeconds(42));
        _links.Fail(first.Id);
        _links.Fail(second.Id);

        var (reloaded, _) = await ReloadAsync();

        Assert.Equal([first, second, Track], reloaded.Queue);
        Assert.Equal(second, reloaded.Current);
        Assert.Equal(TimeSpan.FromSeconds(42), reloaded.Position);
    }

    [Fact]
    public async Task On_reload_only_a_refused_Track_goes_when_others_fail()
    {
        var (first, second) = TwoTracks();
        await _player.PlayAsync([first, second, Track], 0);
        _links.Fail(first.Id);
        _links.Remove(second.Id);

        var (reloaded, _) = await ReloadAsync();

        Assert.Equal([first, Track], reloaded.Queue);
    }

    [Fact]
    public async Task What_a_failed_reload_kept_survives_the_next_save()
    {
        var (first, second) = TwoTracks();
        await _player.PlayAsync([first, second], 0);
        _links.Fail(first.Id);
        _links.Fail(second.Id);
        var (reloaded, _) = await ReloadAsync();

        await reloaded.SetVolumeAsync(0.5);
        _links.Recover(first.Id);
        _links.Recover(second.Id);
        var (again, _) = await ReloadAsync();

        Assert.Equal([first, second], again.Queue);
    }

    [Fact]
    public async Task Pressing_play_while_the_link_fails_gives_a_notice_and_keeps_the_restored_Track_ready()
    {
        await _player.PlayAsync(Track);
        _links.Fail(Track.Id);
        var (reloaded, audio) = await ReloadAsync();

        await reloaded.ResumeAsync();

        Assert.NotNull(reloaded.Notice);
        Assert.Equal(Track, reloaded.Current);

        _links.Recover(Track.Id);
        await reloaded.ResumeAsync();

        Assert.Equal(Link.ToString(), audio.PlayingSource);
    }

    [Fact]
    public async Task Failing_to_get_a_play_link_for_the_next_Track_gives_a_notice_and_keeps_the_current_one()
    {
        var album = Album("Een", "Twee");
        await _player.PlayAsync(album, 0);
        _links.Fail(album[1].Id);

        await _player.NextAsync();

        Assert.Equal(album[0], _player.Current);
        Assert.NotNull(_player.Notice);
    }

    [Fact]
    public async Task Pressing_play_while_restoring_wins_over_the_saved_Queue()
    {
        var (first, second) = TwoTracks();
        await _player.PlayAsync(first);
        var audio = new FakeAudioElement();
        var player = new PlayerService(audio, _links, new RotatingShuffler(), _storage);
        var held = _links.Hold(first.Id);

        var restoring = player.RestoreAsync();
        await player.PlayAsync(second);
        held.SetResult();
        await restoring;

        Assert.Equal(second, player.Current);
        Assert.True(player.IsPlaying);
    }

    [Fact]
    public async Task A_storage_that_cannot_be_read_starts_empty_with_defaults()
    {
        var (reloaded, audio) = await ReloadAsync(new ThrowingPlayerStorage());

        AssertDefaults(reloaded);
        Assert.Null(audio.PlayingSource);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"Queue":[],"CurrentIndex":3,"Volume":0.4}""")]
    [InlineData("""{"Queue":[],"CurrentIndex":-1,"Volume":7}""")]
    [InlineData("""{"Queue":[],"CurrentIndex":-1,"Volume":0.4,"Repeat":9}""")]
    [InlineData("""{"Queue":[{"SourceOrder":0}],"CurrentIndex":0,"Volume":0.4}""")]
    public async Task Saved_state_that_makes_no_sense_starts_empty_with_defaults(string state)
    {
        _storage.State = state;

        var (reloaded, _) = await ReloadAsync();

        AssertDefaults(reloaded);
    }

    [Fact]
    public async Task A_storage_that_cannot_be_written_leaves_the_music_playing()
    {
        var player = new PlayerService(_audio, _links, new RotatingShuffler(), new ThrowingPlayerStorage());
        var unobserved = false;
        EventHandler<UnobservedTaskExceptionEventArgs> watch = (_, _) => unobserved = true;
        TaskScheduler.UnobservedTaskException += watch;

        await player.PlayAsync(Track);
        await player.SetVolumeAsync(0.4);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        TaskScheduler.UnobservedTaskException -= watch;

        Assert.True(player.IsPlaying);
        Assert.Equal(0.4, player.Volume);
        Assert.False(unobserved);
    }

    [Theory]
    [InlineData(RepeatMode.All)]
    [InlineData(RepeatMode.One)]
    public async Task Repeat_comes_back_after_a_reload(RepeatMode repeat)
    {
        await _player.PlayAsync(Track);
        _player.SetRepeat(repeat);

        var (reloaded, _) = await ReloadAsync();

        Assert.Equal(repeat, reloaded.Repeat);
    }

    [Fact]
    public async Task Shuffle_comes_back_after_a_reload_without_shuffling_the_Queue_again()
    {
        var (first, second) = TwoTracks();
        await _player.PlayAsync([first, second, Track], 0);
        _player.SetShuffle(true);

        var (reloaded, _) = await ReloadAsync();

        Assert.True(reloaded.Shuffle);
        Assert.Equal([first, Track, second], reloaded.Queue);
    }

    [Fact]
    public async Task Turning_shuffle_off_after_a_reload_restores_the_original_order()
    {
        var (first, second) = TwoTracks();
        await _player.PlayAsync([first, second, Track], 0);
        _player.SetShuffle(true);
        var (reloaded, _) = await ReloadAsync();

        reloaded.SetShuffle(false);

        Assert.Equal([first, second, Track], reloaded.Queue);
    }

    [Fact]
    public async Task A_Track_added_to_the_Queue_keeps_its_place_when_shuffle_goes_off_after_a_reload()
    {
        var (first, second) = TwoTracks();
        var added = new PlayerTrack(Guid.CreateVersion7(), "Drie", "De Kleingeld");
        _links.Add(added.Id, Link);
        await _player.PlayAsync([first, second, Track], 0);
        _player.SetShuffle(true);
        await _player.AddToQueueAsync(added);
        var (reloaded, _) = await ReloadAsync();

        reloaded.SetShuffle(false);

        Assert.Equal([first, second, Track, added], reloaded.Queue);
    }

    [Fact]
    public async Task Turning_shuffle_off_after_a_reload_that_dropped_a_Track_keeps_the_order_of_the_rest()
    {
        var (first, second) = TwoTracks();
        var third = new PlayerTrack(Guid.CreateVersion7(), "Drie", "De Kleingeld");
        _links.Add(third.Id, Link);
        await _player.PlayAsync([first, second, third, Track], 0);
        _player.SetShuffle(true);
        _links.Remove(third.Id);
        var (reloaded, _) = await ReloadAsync();

        reloaded.SetShuffle(false);

        Assert.Equal([first, second, Track], reloaded.Queue);
    }

    private static void AssertDefaults(PlayerService player)
    {
        Assert.Empty(player.Queue);
        Assert.Null(player.Current);
        Assert.Equal(1, player.Volume);
        Assert.False(player.IsMuted);
        Assert.False(player.Shuffle);
        Assert.Equal(RepeatMode.Off, player.Repeat);
        Assert.False(player.IsPlaying);
    }

    private (PlayerTrack First, PlayerTrack Second) TwoTracks()
    {
        var first = new PlayerTrack(Guid.CreateVersion7(), "Een", "De Kleingeld");
        var second = new PlayerTrack(Guid.CreateVersion7(), "Twee", "De Kleingeld");
        _links.Add(first.Id, Link);
        _links.Add(second.Id, Link);
        return (first, second);
    }

    /// <summary>A new tab's worth of player on the same storage.</summary>
    private async Task<(PlayerService Player, FakeAudioElement Audio)> ReloadAsync(IPlayerStorage? storage = null)
    {
        var audio = new FakeAudioElement();
        var player = new PlayerService(audio, _links, new RotatingShuffler(), storage ?? _storage);
        await player.RestoreAsync();
        return (player, audio);
    }

    private sealed class ThrowingPlayerStorage : IPlayerStorage
    {
        public Task<string?> LoadAsync() => throw new InvalidOperationException("Storage is off.");

        public Task SaveAsync(string state) => throw new InvalidOperationException("Storage is full.");
    }

    private sealed class FakePlayerStorage : IPlayerStorage
    {
        public string? State { get; set; }

        public int Saves { get; private set; }

        public Task<string?> LoadAsync() => Task.FromResult(State);

        public Task SaveAsync(string state)
        {
            State = state;
            Saves++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// The one "random" order the expected Queues are worked out from: the first goes last. Doing it twice doesn't undo
    /// it, so shuffling again can't pass for restoring the order.
    /// </summary>
    private sealed class RotatingShuffler : IShuffler
    {
        public void Shuffle<T>(T[] items)
        {
            if (items.Length > 0)
            {
                var first = items[0];
                Array.Copy(items, 1, items, 0, items.Length - 1);
                items[^1] = first;
            }
        }
    }

    private sealed class FakePlayLinks : IPlayLinkSource
    {
        private readonly Dictionary<Guid, PlayLink> _links = [];
        private readonly Dictionary<Guid, TaskCompletionSource> _held = [];
        private readonly HashSet<Guid> _failing = [];

        public void Add(Guid trackId, Uri link, CoverArt? cover = null) =>
            _links[trackId] = new PlayLink(link, DateTimeOffset.UtcNow.AddMinutes(10), cover);

        public void Remove(Guid trackId) => _links.Remove(trackId);

        /// <summary>Makes asking for the Track's link fail, as a server error or a lost network does.</summary>
        public void Fail(Guid trackId) => _failing.Add(trackId);

        public void Recover(Guid trackId) => _failing.Remove(trackId);

        /// <summary>Keeps the Track's link back until the returned source is completed.</summary>
        public TaskCompletionSource Hold(Guid trackId) => _held[trackId] = new TaskCompletionSource();

        public async Task<PlayLink?> ForTrackAsync(Guid trackId)
        {
            if (_held.TryGetValue(trackId, out var held))
            {
                await held.Task;
            }

            if (_failing.Contains(trackId))
            {
                throw new PlayLinkFailedException("The server had a bad day.");
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

        public Task PlayFromAsync(string source, TimeSpan position)
        {
            PlayingSource = source;
            PlayedFrom = position;
            IsPaused = false;
            return Task.CompletedTask;
        }

        public Task SetVolumeAsync(double volume)
        {
            Volume = volume;
            return Task.CompletedTask;
        }

        public Task SetMutedAsync(bool muted)
        {
            IsMuted = muted;
            return Task.CompletedTask;
        }

        public Task ShowMetadataAsync(MediaMetadata metadata)
        {
            Metadata.Add(metadata);
            return Task.CompletedTask;
        }

        public TimeSpan? SoughtTo { get; private set; }

        public TimeSpan? PlayedFrom { get; private set; }

        public double Volume { get; private set; } = 1;

        public bool IsMuted { get; private set; }

        public event Action<AudioProgress>? Progressed;

        public event Func<MediaCommand, Task>? MediaCommanded;

        public event Func<Task>? Ended;

        public Task EndAsync() => Ended!.Invoke();

        public Task PressAsync(MediaCommand command) => MediaCommanded!.Invoke(command);

        public void Report(AudioProgress progress) => Progressed?.Invoke(progress);
    }
}
