namespace KleinGeld.Web.Client.Player;

/// <summary>What happens when a Track ends: stop at the end of the Queue, wrap to its start, or loop the Track.</summary>
public enum RepeatMode
{
    Off,
    All,
    One,
}
