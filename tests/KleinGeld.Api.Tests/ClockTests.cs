using System.Net.Http.Json;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class ClockTests(KleinGeldApp app)
{
    [Fact]
    public async Task A_test_can_set_the_time_the_system_sees()
    {
        using var client = app.CreateWebClient();
        var instant = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

        await app.SetClockAsync(instant);
        var now = await client.GetFromJsonAsync<DateTimeOffset>("/testing/clock");

        Assert.Equal(instant, now);
    }
}
