using KleinGeld.Contracts;

namespace KleinGeld.Web.Client.Releases;

/// <summary>How a Release's stage reads on its badge.</summary>
public static class StageLabel
{
    public static string For(ReleaseStage stage) => stage == ReleaseStage.TakenDown ? "Taken down" : stage.ToString();
}
