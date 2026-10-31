using KleinGeld.Data;
using KleinGeld.Web;
using KleinGeld.Web.Components;
using KleinGeld.Web.Testing;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddClock();
builder.AddNpgsqlDbContext<KleinGeldDbContext>("kleingeld");
builder.AddAzureBlobServiceClient("blobs");
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseMigratedHealthCheck>("database-migrated");

builder.Services.AddProblemDetails();
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapClockEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(KleinGeld.Web.Client._Imports).Assembly);

app.Run();
