using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace KleinGeld.Web.Client.Uploads;

/// <summary>Sends the file chosen in a file input straight to blob storage, reporting real progress in percent.</summary>
public static class BlobUpload
{
    public static async Task<bool> SendAsync(IJSRuntime js, ElementReference input, Uri link, Action<int> onProgress)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "/blob-upload.js");
        using var progress = DotNetObjectReference.Create(new Progress(onProgress));
        return await module.InvokeAsync<bool>("upload", TimeSpan.FromHours(2), input, link.ToString(), progress);
    }

    private sealed class Progress(Action<int> onProgress)
    {
        [JSInvokable]
        public void Report(int percent) => onProgress(percent);
    }
}
