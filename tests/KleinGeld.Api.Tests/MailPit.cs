using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace KleinGeld.Api.Tests;

/// <summary>Reads the emails the app sent, through MailPit's HTTP API.</summary>
public sealed class MailPit(HttpClient client)
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Waits for the one email to <paramref name="to"/> whose text contains <paramref name="marker"/>, so tests
    /// sharing the one MailPit don't see each other's emails.
    /// </summary>
    public async Task<Email> WaitForEmailAsync(string to, string marker)
    {
        var deadline = DateTimeOffset.UtcNow + WaitTimeout;
        while (true)
        {
            var found = await SearchAsync(to, marker);
            if (found.Count == 1)
            {
                return await ReadAsync(found[0].Id);
            }
            if (found.Count > 1)
            {
                throw new InvalidOperationException($"{found.Count} emails to {to} contain \"{marker}\", expected one.");
            }
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"No email to {to} containing \"{marker}\" arrived within {WaitTimeout}.");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }

    /// <summary>The emails to <paramref name="to"/> whose text contains <paramref name="marker"/>, right now.</summary>
    public async Task<IReadOnlyList<MessageSummary>> SearchAsync(string to, string marker)
    {
        var query = Uri.EscapeDataString($"to:\"{to}\" \"{marker}\"");
        var result = await client.GetFromJsonAsync<SearchResult>($"/api/v1/search?query={query}");
        return result!.Messages;
    }

    private async Task<Email> ReadAsync(string id) =>
        (await client.GetFromJsonAsync<Email>($"/api/v1/message/{id}"))!;

    public sealed record SearchResult([property: JsonPropertyName("messages")] IReadOnlyList<MessageSummary> Messages);

    public sealed record MessageSummary([property: JsonPropertyName("ID")] string Id);

    public sealed record Email(
        [property: JsonPropertyName("Subject")] string Subject,
        [property: JsonPropertyName("Text")] string Text);
}
