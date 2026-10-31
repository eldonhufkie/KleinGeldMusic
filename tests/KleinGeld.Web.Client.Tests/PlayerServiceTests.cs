using KleinGeld.Web.Client.Player;

namespace KleinGeld.Web.Client.Tests;

public sealed class PlayerServiceTests
{
    [Fact]
    public async Task Playing_a_Track_starts_its_audio_and_reports_playing()
    {
        var audio = new FakeAudioElement();
        var player = new PlayerService(audio);
        var changes = 0;
        player.Changed += () => changes++;

        await player.PlayAsync("https://blobs.example/track.m4a");

        Assert.Equal("https://blobs.example/track.m4a", audio.PlayingSource);
        Assert.True(player.IsPlaying);
        Assert.Equal(1, changes);
    }

    private sealed class FakeAudioElement : IAudioElement
    {
        public string? PlayingSource { get; private set; }

        public Task PlayAsync(string source)
        {
            PlayingSource = source;
            return Task.CompletedTask;
        }
    }
}
