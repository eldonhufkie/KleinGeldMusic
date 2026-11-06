using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KleinGeld.Web.Testing;

/// <summary>
/// Hooks for the API tests. Only active in Development with <c>Testing:Enabled</c> on, which nothing but the
/// test fixture sets.
/// </summary>
internal static class TestingEndpoints
{
    private const string ConfigurationKey = "Testing:Enabled";

    /// <summary>Whether this host runs for the API tests.</summary>
    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment) =>
        environment.IsDevelopment() && configuration.GetValue<bool>(ConfigurationKey);

    public static void MapTestingEndpoints(this WebApplication app)
    {
        if (!IsEnabled(app.Configuration, app.Environment))
        {
            return;
        }

        var testing = app.MapGroup("/testing");

        testing.MapGet("/clock", (TimeProvider clock) => clock.GetUtcNow());
        testing.MapPut("/clock", (ControllableClock clock, [FromBody] DateTimeOffset now) =>
        {
            clock.Set(now);
            return Results.NoContent();
        });

        testing.MapPost("/upload-clean-up", async (UploadCleanUp cleanUp) =>
        {
            await cleanUp.RunAsync();
            return Results.NoContent();
        });

        // A list needs more Releases than going through upload, conversion and approval for each would allow.
        testing.MapPost("/published-releases", async (KleinGeldDbContext db, [FromBody] SeededReleases seeded) =>
        {
            db.Releases.AddRange(seeded.Releases.Select(r => new Release
            {
                ArtistId = seeded.ArtistId,
                Title = r.Title,
                Slug = "seeded",
                Type = seeded.Type,
                ReleaseDate = r.ReleaseDate,
                Stage = ReleaseStage.Published,
                PublishedAt = r.PublishedAt,
            }));
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        testing.MapPost("/security-stamp",async (UserManager<Member> members, [FromBody] string email) =>
        {
            var member = await members.FindByEmailAsync(email);
            if (member is null)
            {
                return Results.NotFound();
            }

            await members.UpdateSecurityStampAsync(member);
            return Results.NoContent();
        });
    }

    private sealed record SeededRelease(string Title, DateTimeOffset PublishedAt, DateOnly? ReleaseDate);

    private sealed record SeededReleases(Guid ArtistId, ReleaseType Type, List<SeededRelease> Releases);
}
