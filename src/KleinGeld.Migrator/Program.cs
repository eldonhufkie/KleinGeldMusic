using KleinGeld.Data;
using Microsoft.EntityFrameworkCore;

// Applies EF Core migrations once and exits; the web host waits for it to complete.
var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<KleinGeldDbContext>("kleingeld");

using var host = builder.Build();
using var scope = host.Services.CreateScope();
await scope.ServiceProvider.GetRequiredService<KleinGeldDbContext>().Database.MigrateAsync();
