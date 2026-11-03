using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes;
using Microsoft.Extensions.Configuration;

namespace KleinGeld.AppHost;

/// <summary>The two copies of the app on TrueNAS (see GLOSSARY.md).</summary>
internal enum Copy
{
    Production,
    Test,
}

/// <summary>
/// Which copy of the app `aspire publish` writes the compose file for, and where its datasets are.
/// Set at publish time, e.g. <c>Deploy__Copy=Test Deploy__Pool=tank aspire publish</c>.
/// </summary>
/// <param name="DataPath">The copy's dataset on the host, <c>/mnt/&lt;pool&gt;/kleingeld/production</c> or <c>…/test</c>.</param>
internal sealed record Deployment(Copy Copy, string DataPath)
{
    public const string RestartUnlessStopped = "unless-stopped";

    /// <summary>Images are pulled from GHCR; release.env names the tag (the commit SHA CI pushed).</summary>
    public const string ImageRegistry = "ghcr.io/eldonhufkie";

    public const string ImageTagVariable = "KLEINGELD_TAG";

    /// <summary>Azurite's well-known development account; Test holds no real data.</summary>
    public const string AzuriteAccount =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;";

    public static Deployment FromConfiguration(IConfiguration configuration)
    {
        var copyName = configuration["Deploy:Copy"] ?? nameof(Copy.Production);
        if (!Enum.TryParse<Copy>(copyName, ignoreCase: true, out var copy))
        {
            throw new InvalidOperationException($"Deploy:Copy must be Production or Test, not '{copyName}'.");
        }
        var pool = configuration["Deploy:Pool"];
        if (string.IsNullOrWhiteSpace(pool))
        {
            throw new InvalidOperationException("Deploy:Pool (the TrueNAS pool holding the kleingeld datasets) is required to publish.");
        }
        return new Deployment(copy, $"/mnt/{pool}/Applications/kleingeld/{copy.ToString().ToLowerInvariant()}");
    }

    /// <summary>
    /// Host ports, bound to 127.0.0.1 only: Tailscale Funnel (Production) or serve (Test) fronts them.
    /// Different per copy so both can run side by side: web 8080/8081, Test's MailPit 8025, Test's Azurite 10000.
    /// </summary>
    public int WebHostPort => Copy == Copy.Production ? 8080 : 8081;

    public string Image(string name) => $"{ImageRegistry}/kleingeld-{name}:${{{ImageTagVariable}:?set {ImageTagVariable} in release.env}}";

    /// <summary>Restart unless stopped, with an optional host folder on the copy's dataset (so ZFS snapshots cover it).</summary>
    public void LongRunning(Service service, string? dataset = null, string? target = null)
    {
        service.Restart = RestartUnlessStopped;
        if (dataset is not null && target is not null)
        {
            service.Volumes.Add(new Volume { Name = dataset, Type = "bind", Source = $"{DataPath}/{dataset}", Target = target, ReadOnly = false });
        }
    }

    /// <summary>Publishes a container port on the host's loopback only.</summary>
    public static void PublishLocally(Service service, int hostPort, int containerPort)
    {
        service.Ports.Clear();
        service.Ports.Add($"127.0.0.1:{hostPort}:{containerPort}");
    }
}
