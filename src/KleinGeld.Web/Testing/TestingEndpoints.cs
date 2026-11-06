using KleinGeld.Contracts;
using KleinGeld.Data;
using KleinGeld.Web.Blobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        // Going through approval takes seconds per Release, too slow for a list longer than one load.
        testing.MapPost("/published-artists", async (KleinGeldDbContext db, [FromBody] string[] names) =>
        {
            var manager = await db.Users.FirstAsync();
            foreach (var name in names)
            {
                var artist = new Artist { Name = name, Slug = Slugs.From(name), Managers = [manager] };
                artist.Releases.Add(new Release
                {
                    Title = name,
                    Slug = Slugs.From(name),
                    Type = ReleaseType.Single,
                    ReleaseDate = new DateOnly(2026, 1, 1),
                    Stage = ReleaseStage.Published,
                });
                db.Artists.Add(artist);
            }

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
}
