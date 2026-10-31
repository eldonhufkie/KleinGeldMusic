namespace KleinGeld.Web.Client.Player;

/// <summary>
/// The browser's single <c>&lt;audio&gt;</c> element, driven through a small JS module.
/// The seam that lets <see cref="PlayerService"/> be tested without a browser.
/// </summary>
public interface IAudioElement
{
    Task PlayAsync(string source);
}
