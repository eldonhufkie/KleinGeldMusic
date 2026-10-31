using KleinGeld.Data;
using KleinGeld.Migrator;
using Microsoft.EntityFrameworkCore;

// Applies EF Core migrations and seeds the Admin once, then exits; the web host waits for it to complete.
var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<KleinGeldDbContext>("kleingeld");
builder.Services.AddMemberIdentityCore();
builder.Services.AddScoped<AdminSeeder>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();
await scope.ServiceProvider.GetRequiredService<KleinGeldDbContext>().Database.MigrateAsync();
await scope.ServiceProvider.GetRequiredService<AdminSeeder>().EnsureAdminAsync();
