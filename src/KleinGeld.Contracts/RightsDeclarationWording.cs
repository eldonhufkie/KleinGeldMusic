namespace KleinGeld.Contracts;

/// <summary>
/// The fixed text of the Rights Declaration checkbox. Changing the text means raising <see cref="Version"/>, because
/// each submission records the version its Member ticked.
/// </summary>
public static class RightsDeclarationWording
{
    public const int Version = 1;

    public const string Text = "I declare that I own the rights to this music, or have permission to publish it on KleinGeld Music.";
}
