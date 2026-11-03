using KleinGeld.Data;
using KleinGeld.Migrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

// Applies EF Core migrations and seeds the Admin once, then exits; the web host waits for it to complete.
var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
// Retries transient failures (EnableRetryOnFailure), e.g. PostgreSQL still starting after a host reboot.
builder.AddNpgsqlDbContext<KleinGeldDbContext>("kleingeld", settings => settings.DisableRetry = false);
builder.Services.AddMemberIdentityCore();
builder.Services.AddScoped<AdminSeeder>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var database = scope.ServiceProvider.GetRequiredService<KleinGeldDbContext>().Database;

// The migrator runs once (restart "no") and the web host waits for it, so after a reboot it must outlast a
// slow PostgreSQL rather than give up after the retry strategy's few attempts.
// ExistsAsync talks to the server (not the kleingeld database, which the first migration may create).
var creator = database.GetService<IRelationalDatabaseCreator>();
var deadline = DateTimeOffset.UtcNow.AddMinutes(5);
while (true)
{
    try
    {
        await creator.ExistsAsync();
        break;
    }
    catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException && DateTimeOffset.UtcNow < deadline)
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}

await database.MigrateAsync();
await scope.ServiceProvider.GetRequiredService<AdminSeeder>().EnsureAdminAsync();
