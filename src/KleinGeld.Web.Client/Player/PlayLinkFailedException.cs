namespace KleinGeld.Web.Client.Player;

/// <summary>
/// No answer about a play link: the network, the server or the session failed. Unlike a refusal, this says nothing
/// about whether the Track can still be played.
/// </summary>
public sealed class PlayLinkFailedException(string message, Exception? cause = null) : Exception(message, cause);
