// PROTOTYPE variant A: one PlayerService holds player state (the dotnet-podcasts pattern).
// Registered scoped, which in WebAssembly means one per browser tab; the server copy only serves prerendering.
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Prototype;

namespace AWasm.Client;

public sealed class PlayerService(IJSRuntime js) : IAsyncDisposable
{
    IJSObjectReference? module;
    DotNetObjectReference<PlayerService>? self;

    public List<QueueItem> Queue { get; private set; } = [];
    public int Index { get; private set; } = -1;
    public QueueItem? Current => Index >= 0 && Index < Queue.Count ? Queue[Index] : null;
    public bool IsPlaying { get; private set; }
    public double Position { get; private set; }
    public double Duration { get; private set; }
    public string? AudioBornAt { get; private set; }
    public double? InteractiveAtMs { get; private set; }

    public event Action? Changed;

    public async Task AttachAsync(ElementReference audio)
    {
        module = await js.InvokeAsync<IJSObjectReference>("import", "./player.js");
        self = DotNetObjectReference.Create(this);
        var info = await module.InvokeAsync<AttachInfo>("attach", audio, self);
        (AudioBornAt, InteractiveAtMs) = (info.BornAt, info.NowMs);
        Changed?.Invoke();
    }

    public async Task PlayAsync(List<QueueItem> queue, int index)
    {
        (Queue, Index) = (queue, index);
        await LoadCurrentAsync();
    }

    public async Task ToggleAsync()
    {
        if (module is not null) await module.InvokeVoidAsync(IsPlaying ? "pause" : "play");
    }

    public async Task NextAsync()
    {
        if (Index + 1 >= Queue.Count) return;
        Index++;
        await LoadCurrentAsync();
    }

    public async Task PreviousAsync()
    {
        if (Position > 3 || Index <= 0) { await SeekAsync(0); return; }
        Index--;
        await LoadCurrentAsync();
    }

    public async Task SeekAsync(double seconds)
    {
        if (module is not null) await module.InvokeVoidAsync("seek", seconds);
    }

    async Task LoadCurrentAsync()
    {
        if (module is null || Current is null) return;
        Position = 0;
        await module.InvokeVoidAsync("load", Current.AudioUrl);
        Changed?.Invoke();
    }

    [JSInvokable] public void OnTime(double position, double duration) { (Position, Duration) = (position, duration); Changed?.Invoke(); }
    [JSInvokable] public void OnPlayState(bool playing) { IsPlaying = playing; Changed?.Invoke(); }
    [JSInvokable] public Task OnEnded() => NextAsync();

    public async ValueTask DisposeAsync()
    {
        if (module is not null) await module.DisposeAsync();
        self?.Dispose();
    }

    record AttachInfo(string BornAt, double NowMs);
}
