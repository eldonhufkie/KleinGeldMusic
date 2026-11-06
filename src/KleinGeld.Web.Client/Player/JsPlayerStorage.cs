using Microsoft.JSInterop;

namespace KleinGeld.Web.Client.Player;

/// <summary>The real <see cref="IPlayerStorage"/>. <c>wwwroot/logout.js</c> removes the same key on logout.</summary>
public sealed class JsPlayerStorage(IJSRuntime js) : IPlayerStorage
{
    private const string Key = "kleingeld.player";

    public async Task<string?> LoadAsync() => await js.InvokeAsync<string?>("localStorage.getItem", Key);

    public async Task SaveAsync(string state) => await js.InvokeVoidAsync("localStorage.setItem", Key, state);
}
