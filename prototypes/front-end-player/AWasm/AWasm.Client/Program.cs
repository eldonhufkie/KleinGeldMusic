using AWasm.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Prototype;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<PlaylistStore>();

await builder.Build().RunAsync();
