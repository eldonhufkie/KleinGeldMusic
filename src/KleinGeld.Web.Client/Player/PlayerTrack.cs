namespace KleinGeld.Web.Client.Player;

/// <summary>What the player needs to know about a Track. It holds the id, never a link.</summary>
public sealed record PlayerTrack(Guid Id, string Title);
