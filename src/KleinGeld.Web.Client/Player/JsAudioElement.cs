using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace KleinGeld.Web.Client.Player;

/// <summary>The real <see cref="IAudioElement"/>: <c>wwwroot/player.js</c> driving the element in the player bar.</summary>
public sealed class JsAudioElement(IJSRuntime js) : IAudioElement, IAsyncDisposable
{
    private IJSObjectReference? _module;
    private DotNetObjectReference<JsAudioElement>? _self;

    public event Action<AudioProgress>? Progressed;

    public async Task AttachAsync(ElementReference element)
    {
        _self = DotNetObjectReference.Create(this);
        _module = await js.InvokeAsync<IJSObjectReference>("import", "/player.js");
        await _module.InvokeVoidAsync("attach", element, _self);
    }

    public Task PlayAsync(string source) => Module.InvokeVoidAsync("play", source).AsTask();

    public Task PauseAsync() => Module.InvokeVoidAsync("pause").AsTask();

    public Task ResumeAsync() => Module.InvokeVoidAsync("resume").AsTask();

    public Task SeekAsync(TimeSpan position) => Module.InvokeVoidAsync("seek", position.TotalSeconds).AsTask();

    [JSInvokable]
    public void Report(double position, double duration, bool isPlaying) =>
        Progressed?.Invoke(new AudioProgress(TimeSpan.FromSeconds(position), TimeSpan.FromSeconds(duration), isPlaying));

    public async ValueTask DisposeAsync()
    {
        _self?.Dispose();
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    private IJSObjectReference Module =>
        _module ?? throw new InvalidOperationException("The player bar hasn't attached the audio element.");
}
