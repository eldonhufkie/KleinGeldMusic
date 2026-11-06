namespace KleinGeld.Web.Client.Player;

/// <summary>The randomness behind shuffle, a seam so <see cref="PlayerService"/> can be tested with a known order.</summary>
public interface IShuffler
{
    void Shuffle<T>(T[] items);
}

public sealed class RandomShuffler : IShuffler
{
    public void Shuffle<T>(T[] items) => Random.Shared.Shuffle(items);
}
