using System.Net;
using System.Text;
using KleinGeld.Web.Client.Player;

namespace KleinGeld.Web.Client.Tests;

public sealed class ApiPlayLinkSourceTests
{
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task A_refusal_from_the_API_means_no_link(HttpStatusCode status)
    {
        var source = SourceAnswering(() => new HttpResponseMessage(status));

        Assert.Null(await source.ForTrackAsync(Guid.CreateVersion7()));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Any_other_failed_answer_is_not_a_refusal(HttpStatusCode status)
    {
        var source = SourceAnswering(() => new HttpResponseMessage(status));

        await Assert.ThrowsAsync<PlayLinkFailedException>(() => source.ForTrackAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task A_network_error_is_not_a_refusal()
    {
        var source = SourceAnswering(() => throw new HttpRequestException("No route to host."));

        await Assert.ThrowsAsync<PlayLinkFailedException>(() => source.ForTrackAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task A_request_that_times_out_is_not_a_refusal()
    {
        var source = SourceAnswering(() => throw new TaskCanceledException());

        await Assert.ThrowsAsync<PlayLinkFailedException>(() => source.ForTrackAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task An_answer_that_is_no_play_link_is_not_a_refusal()
    {
        var source = SourceAnswering(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>", Encoding.UTF8, "application/json"),
        });

        await Assert.ThrowsAsync<PlayLinkFailedException>(() => source.ForTrackAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task A_play_link_comes_back_as_it_was_sent()
    {
        var source = SourceAnswering(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"url":"https://blobs.example/a.m4a","expiresAt":"2030-01-01T00:00:00+00:00"}""",
                Encoding.UTF8,
                "application/json"),
        });

        var link = await source.ForTrackAsync(Guid.CreateVersion7());

        Assert.Equal(new Uri("https://blobs.example/a.m4a"), link?.Url);
    }

    private static ApiPlayLinkSource SourceAnswering(Func<HttpResponseMessage> answer) =>
        new(new HttpClient(new StubHandler(answer)) { BaseAddress = new Uri("https://kleingeld.example/") });

    private sealed class StubHandler(Func<HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer());
    }
}
