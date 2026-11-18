using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace KleinGeld.Web.Client.Uploads;

/// <summary>Sends a file chosen in a file input straight to blob storage, reporting real progress in percent.</summary>
public static class BlobUpload
{
    public static async Task<bool> SendAsync(IJSRuntime js, ElementReference input, Uri link, Action<int> onProgress, int fileIndex = 0)
    {
        await using var module = await ImportAsync(js);
        using var progress = DotNetObjectReference.Create(new Progress(onProgress));
        return await module.InvokeAsync<bool>("upload", TimeSpan.FromHours(2), [input, link.ToString(), progress, fileIndex]);
    }

    /// <summary>The files chosen in a multiple file input, in the order <c>fileIndex</c> counts them.</summary>
    public static async Task<List<ChosenFile>> ChosenFilesAsync(IJSRuntime js, ElementReference input)
    {
        await using var module = await ImportAsync(js);
        return await module.InvokeAsync<List<ChosenFile>>("chosenFiles", input);
    }

    public static async Task ClearAsync(IJSRuntime js, ElementReference input)
    {
        await using var module = await ImportAsync(js);
        await module.InvokeVoidAsync("clear", input);
    }

    private static ValueTask<IJSObjectReference> ImportAsync(IJSRuntime js) =>
        js.InvokeAsync<IJSObjectReference>("import", "/blob-upload.js");

    public sealed record ChosenFile(string Name, long Size);

    private sealed class Progress(Action<int> onProgress)
    {
        [JSInvokable]
        public void Report(int percent) => onProgress(percent);
    }
}
