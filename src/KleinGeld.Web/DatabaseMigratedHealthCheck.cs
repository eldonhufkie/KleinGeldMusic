using KleinGeld.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KleinGeld.Web;

/// <summary>
/// Unhealthy while the database is behind the code's migrations, e.g. if the migrator didn't run.
/// </summary>
internal sealed class DatabaseMigratedHealthCheck(KleinGeldDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        return pending.Any()
            ? HealthCheckResult.Unhealthy($"Pending migrations: {string.Join(", ", pending)}")
            : HealthCheckResult.Healthy();
    }
}
