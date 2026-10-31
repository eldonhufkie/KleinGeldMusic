using KleinGeld.Data;
using KleinGeld.Web;
using KleinGeld.Web.Api;
using KleinGeld.Web.Components;
using KleinGeld.Web.Testing;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddClock();
builder.AddNpgsqlDbContext<KleinGeldDbContext>("kleingeld");
builder.AddAzureBlobServiceClient("blobs");
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseMigratedHealthCheck>("database-migrated");

builder.AddMemberAuthentication();

builder.Services.AddProblemDetails();
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents()
    // Sends the name and role claims to the client (enough for the Admin link), never the security stamp.
    .AddAuthenticationStateSerialization();

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
// Pages get the Not found page; API calls keep their bare status codes.
app.UseWhen(
    context => !context.Request.IsApiRequest(),
    pages => pages.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapTestingEndpoints();
app.MapGroup("/api").RequireAuthorization().MapMemberEndpoints();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(KleinGeld.Web.Client._Imports).Assembly);

// Endpoints the Identity /Account pages post to (logout, passkeys, personal data download).
app.MapAdditionalIdentityEndpoints();

app.Run();
