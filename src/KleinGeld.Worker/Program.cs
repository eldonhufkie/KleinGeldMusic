using KleinGeld.Data;
using KleinGeld.Worker;

// Converts uploaded Tracks in the background. Exactly one runs: see ConversionWorker.
var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<KleinGeldDbContext>("kleingeld", settings => settings.DisableRetry = false);
builder.AddAzureBlobServiceClient("blobs");
builder.Services.AddSingleton<AudioConverter>();
builder.Services.AddHostedService<ConversionWorker>();

await builder.Build().RunAsync();
