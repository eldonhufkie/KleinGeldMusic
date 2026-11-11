using KleinGeld.Web.Client.Artists;
using KleinGeld.Web.Client.Player;
using KleinGeld.Web.Client.Playlists;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

// Calls the API on the same origin, so the login cookie goes along.
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<IManagedArtistsState, ManagedArtistsState>();
builder.Services.AddScoped<IReviewCountState, ReviewCountState>();
builder.Services.AddScoped<IPlaylistsState, PlaylistsState>();
builder.Services.AddScoped<IPlayLinkSource, ApiPlayLinkSource>();
builder.Services.AddPlayer();

await builder.Build().RunAsync();
