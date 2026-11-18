namespace KleinGeld.Web.Client.Player;

/// <summary>The API no longer knows this Member: the login has gone, so no Track can be played until they log in again.</summary>
public sealed class SessionEndedException() : PlayLinkFailedException("The session has ended.");
