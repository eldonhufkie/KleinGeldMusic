namespace KleinGeld.Web.Client.Player;

/// <summary>
/// Holds the playback state for this browser tab and raises <see cref="Changed"/> whenever it changes.
/// </summary>
public sealed class PlayerService(IAudioElement audio)
{
    public event Action? Changed;

    public bool IsPlaying { get; private set; }

    public async Task PlayAsync(string source)
    {
        await audio.PlayAsync(source);
        IsPlaying = true;
        Changed?.Invoke();
    }
}
