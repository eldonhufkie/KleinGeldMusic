using CStaticSsr.Components;
using Prototype;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddSingleton<PlaylistStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/audio/{id:int}.wav", (int id) =>
    SampleAudio.Get(id) is { } wav ? Results.File(wav, "audio/wav", enableRangeProcessing: true) : Results.NotFound());
app.MapRazorComponents<App>();

app.Run();
