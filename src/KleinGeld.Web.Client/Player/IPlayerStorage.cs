namespace KleinGeld.Web.Client.Player;

/// <summary>Where this browser keeps the player's saved state. <c>localStorage</c> in the browser, a fake in tests.</summary>
public interface IPlayerStorage
{
    /// <summary>What was saved last, or null when nothing was.</summary>
    Task<string?> LoadAsync();

    Task SaveAsync(string state);
}
