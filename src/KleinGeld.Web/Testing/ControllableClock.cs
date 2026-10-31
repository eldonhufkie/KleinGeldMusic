using Microsoft.AspNetCore.Mvc;

namespace KleinGeld.Web.Testing;

/// <summary>
/// A <see cref="TimeProvider"/> that API tests can set, so rules like link expiry can be tested
/// without waiting. Only registered when <c>Testing:ControllableClock</c> is on in Development.
/// </summary>
internal sealed class ControllableClock : TimeProvider
{
    private DateTimeOffset? _now;

    public override DateTimeOffset GetUtcNow() => _now ?? System.GetUtcNow();

    public void Set(DateTimeOffset now) => _now = now.ToUniversalTime();
}

internal static class ControllableClockExtensions
{
    private const string ConfigurationKey = "Testing:ControllableClock";

    /// <summary>Registers the system clock, or a <see cref="ControllableClock"/> when tests ask for one.</summary>
    public static void AddClock(this WebApplicationBuilder builder)
    {
        if (IsEnabled(builder.Configuration, builder.Environment))
        {
            var clock = new ControllableClock();
            builder.Services.AddSingleton(clock);
            builder.Services.AddSingleton<TimeProvider>(clock);
        }
        else
        {
            builder.Services.AddSingleton(TimeProvider.System);
        }
    }

    /// <summary>Maps <c>/testing/clock</c> for reading and setting the time, only alongside a <see cref="ControllableClock"/>.</summary>
    public static void MapClockEndpoints(this WebApplication app)
    {
        if (!IsEnabled(app.Configuration, app.Environment))
        {
            return;
        }

        app.MapGet("/testing/clock", (TimeProvider clock) => clock.GetUtcNow());
        app.MapPut("/testing/clock", (ControllableClock clock, [FromBody] DateTimeOffset now) =>
        {
            clock.Set(now);
            return Results.NoContent();
        });
    }

    private static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment) =>
        environment.IsDevelopment() && configuration.GetValue<bool>(ConfigurationKey);
}
