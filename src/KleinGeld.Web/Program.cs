using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web;
using KleinGeld.Web.Api;
using KleinGeld.Web.Blobs;
using KleinGeld.Web.Components;
using KleinGeld.Web.Email;
using KleinGeld.Web.Testing;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddClock();
// Retries transient failures (EnableRetryOnFailure), e.g. PostgreSQL still starting after a host reboot.
builder.AddNpgsqlDbContext<KleinGeldDbContext>("kleingeld", settings => settings.DisableRetry = false);
builder.AddAzureBlobServiceClient("blobs");
builder.Services.AddHostedService<BlobCorsSetup>();
builder.Services.AddSingleton<BlobLinks>();
builder.Services.AddSingleton<CoverStore>();
builder.Services.AddSingleton<AudioProbe>();
builder.Services.AddSingleton<UploadCleanUp>();
builder.Services.AddHostedService<DailyUploadCleanUp>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseMigratedHealthCheck>("database-migrated");

builder.AddMemberAuthentication();
builder.AddEmailSender();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<KleinGeld.Web.Client.Artists.IManagedArtistsState, ServerManagedArtistsState>();

builder.Services.AddProblemDetails();
// Checks API request bodies against their DataAnnotations and answers 400 with the errors.
builder.Services.AddValidation();
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
app.MapGroup("/api").MapJoinRequestEndpoints();
app.MapGroup("/api").RequireAuthorization().MapMemberEndpoints();
app.MapGroup("/api").RequireAuthorization().MapArtistEndpoints();
app.MapGroup("/api").RequireAuthorization().MapReleaseEndpoints();
app.MapGroup("/api").RequireAuthorization().MapTrackEndpoints();
app.MapGroup("/api").RequireAuthorization().MapCoverEndpoints();
var adminApi = app.MapGroup("/api/admin").RequireAuthorization(admin => admin.RequireRole(Roles.Admin));
adminApi.MapAdminJoinRequestEndpoints();
adminApi.MapAdminMemberEndpoints();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(KleinGeld.Web.Client._Imports).Assembly);

// Endpoints the Identity /Account pages post to (logout, passkeys, personal data download).
app.MapAdditionalIdentityEndpoints();

app.Run();
