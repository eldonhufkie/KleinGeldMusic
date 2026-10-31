using AWasm.Client;
using AWasm.Components;
using Prototype;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Prerendering runs the Client pages on the server too, so they need these here as well.
builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<PlaylistStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/audio/{id:int}.wav", (int id) =>
    SampleAudio.Get(id) is { } wav ? Results.File(wav, "audio/wav", enableRangeProcessing: true) : Results.NotFound());
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AWasm.Client._Imports).Assembly);

app.Run();
