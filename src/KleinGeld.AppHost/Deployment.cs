using Aspire.Hosting.Docker.Resources.ServiceNodes;
using Microsoft.Extensions.Configuration;

namespace KleinGeld.AppHost;

/// <summary>
/// Which copy of the app on TrueNAS `aspire publish` writes the compose file for, and where its datasets are.
/// Set at publish time, e.g. <c>Deploy__Copy=Test Deploy__Pool=tank aspire publish</c>.
/// </summary>
/// <param name="IsProduction">Production (real Azure Blob and SMTP) or Test (Azurite and MailPit).</param>
/// <remarks>PostgreSQL 18 images keep their data under <c>/var/lib/postgresql</c>.</remarks>
/// <param name="DataPath">The copy's dataset on the host, <c>/mnt/&lt;pool&gt;/kleingeld/production</c> or <c>…/test</c>.</param>
internal sealed record Deployment(bool IsProduction, string DataPath)
{
    public const string RestartAlways = "unless-stopped";

    /// <summary>Images are pulled from GHCR; release.env names the tag (the commit SHA CI pushed).</summary>
    public const string ImageRegistry = "ghcr.io/eldonhufkie";

    public const string ImageTagVariable = "KLEINGELD_TAG";

    /// <summary>Azurite's well-known development account; Test holds no real data.</summary>
    public const string AzuriteAccount =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;";

    public static Deployment FromConfiguration(IConfiguration configuration)
    {
        var copy = configuration["Deploy:Copy"] ?? "Production";
        var isProduction = copy.ToLowerInvariant() switch
        {
            "production" => true,
            "test" => false,
            _ => throw new InvalidOperationException($"Deploy:Copy must be Production or Test, not '{copy}'."),
        };
        var pool = configuration["Deploy:Pool"];
        if (string.IsNullOrWhiteSpace(pool))
        {
            throw new InvalidOperationException("Deploy:Pool (the TrueNAS pool holding the kleingeld datasets) is required to publish.");
        }
        return new Deployment(isProduction, $"/mnt/{pool}/kleingeld/{(isProduction ? "production" : "test")}");
    }

    /// <summary>The host port the web host is published on: 8080 for Production, 8081 for Test.</summary>
    public int WebHostPort => IsProduction ? 8080 : 8081;

    public string Image(string name) => $"{ImageRegistry}/kleingeld-{name}:${{{ImageTagVariable}:?set {ImageTagVariable} in release.env}}";

    /// <summary>A host folder on the copy's dataset, so ZFS snapshots cover it.</summary>
    public Volume BindMount(string dataset, string target) =>
        new() { Name = dataset, Type = "bind", Source = $"{DataPath}/{dataset}", Target = target, ReadOnly = false };
}
