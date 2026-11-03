namespace KleinGeld.Web.Testing;

/// <summary>
/// A <see cref="TimeProvider"/> that API tests can set, so rules like link expiry can be tested
/// without waiting. Only registered when the testing hooks are on (see <see cref="TestingEndpoints"/>).
/// </summary>
internal sealed class ControllableClock : TimeProvider
{
    private DateTimeOffset? _now;

    public override DateTimeOffset GetUtcNow() => _now ?? System.GetUtcNow();

    public void Set(DateTimeOffset now) => _now = now.ToUniversalTime();
}

internal static class ControllableClockExtensions
{
    /// <summary>Registers the system clock, or a <see cref="ControllableClock"/> when tests ask for one.</summary>
    public static void AddClock(this WebApplicationBuilder builder)
    {
        if (TestingEndpoints.IsEnabled(builder.Configuration, builder.Environment))
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
}
