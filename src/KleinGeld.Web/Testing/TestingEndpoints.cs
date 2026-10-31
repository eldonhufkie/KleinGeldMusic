using KleinGeld.Data;
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

        testing.MapPost("/security-stamp", async (UserManager<Member> members, [FromBody] string email) =>
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
