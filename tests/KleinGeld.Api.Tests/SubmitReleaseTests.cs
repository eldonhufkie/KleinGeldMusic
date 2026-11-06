using System.Net.Http.Headers;
using System.Net.Http.Json;
using KleinGeld.Contracts;
using SkiaSharp;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed partial class SubmitReleaseTests(KleinGeldApp app) : IAsyncLifetime
{
    private static readonly HttpClient BlobStorage = new();
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromMinutes(1);

    // Earlier tests leave the clock anywhere; storage checks links against the real time.
    public Task InitializeAsync() => app.SetClockAsync(DateTimeOffset.UtcNow);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_complete_Draft_with_a_Rights_Declaration_becomes_Submitted()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateCompleteDraftAsync(manager);

        using var submitted = await SubmitAsync(manager, release);

        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        Assert.Equal(ReleaseStage.Submitted, (await ReadAsync(manager, release)).Stage);
    }

    [Fact]
    public async Task Submit_without_ticking_the_Rights_Declaration_is_refused()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateCompleteDraftAsync(manager);

        using var submitted = await SubmitAsync(manager, release, rightsDeclared: false);

        await AssertRefusedAsync(submitted, HttpStatusCode.BadRequest, "Rights Declaration");
        Assert.Equal(ReleaseStage.Draft, (await ReadAsync(manager, release)).Stage);
    }

    [Fact]
    public async Task A_Release_without_a_cover_cannot_be_submitted()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager, CompleteForm());
        await WaitUntilConvertedAsync(manager, release, await AddTrackAsync(manager, release, "tone.flac"));

        using var submitted = await SubmitAsync(manager, release);

        await AssertRefusedAsync(submitted, HttpStatusCode.Conflict, "cover");
        Assert.Equal(ReleaseStage.Draft, (await ReadAsync(manager, release)).Stage);
    }

    [Fact]
    public async Task A_Release_without_Tracks_cannot_be_submitted()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateDraftAsync(manager, CompleteForm());
        await SetCoverAsync(manager, release);

        using var submitted = await SubmitAsync(manager, release);

        await AssertRefusedAsync(submitted, HttpStatusCode.Conflict, "at least one Track");
    }

    [Fact]
    public async Task A_Release_with_a_Failed_Track_cannot_be_submitted()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateCompleteDraftAsync(manager);
        var corrupt = await AddTrackAsync(manager, release, "corrupt.flac");
        Assert.Equal(TrackStatus.Failed, await WaitUntilConvertedAsync(manager, release, corrupt));

        using var submitted = await SubmitAsync(manager, release);

        await AssertRefusedAsync(submitted, HttpStatusCode.Conflict, "every Track");
    }

    [Fact]
    public async Task A_Release_with_a_Track_still_uploading_cannot_be_submitted()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateCompleteDraftAsync(manager);
        using var added = await manager.PostAsJsonAsync($"/api/releases/{release.Id}/tracks", new TrackUploadForm { FileName = "later.flac", Size = 1000 });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        using var submitted = await SubmitAsync(manager, release);

        await AssertRefusedAsync(submitted, HttpStatusCode.Conflict, "every Track");
    }

    [Theory]
    [InlineData("", "2026-01-01", "title")]
    [InlineData("Zonder Datum", null, "release date")]
    public async Task A_Release_with_a_blank_title_or_date_cannot_be_submitted(string title, string? date, string reason)
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateCompleteDraftAsync(manager);
        using var edited = await manager.PutAsJsonAsync($"/api/releases/{release.Id}", new ReleaseForm
        {
            Title = title,
            Type = ReleaseType.Single,
            ReleaseDate = date is null ? null : DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture),
        });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

        using var submitted = await SubmitAsync(manager, release);

        await AssertRefusedAsync(submitted, HttpStatusCode.Conflict, reason);
    }

    [Fact]
    public async Task A_Submitted_Release_cannot_be_submitted_again()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateCompleteDraftAsync(manager);
        using var first = await SubmitAsync(manager, release);

        using var second = await SubmitAsync(manager, release);

        await AssertRefusedAsync(second, HttpStatusCode.Conflict, "Draft or Rejected");
    }

    [Fact]
    public async Task Withdraw_returns_a_Submitted_Release_to_Draft()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateCompleteDraftAsync(manager);
        using var submitted = await SubmitAsync(manager, release);

        using var withdrawn = await WithdrawAsync(manager, release);

        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal(ReleaseStage.Draft, (await ReadAsync(manager, release)).Stage);
    }

    [Fact]
    public async Task Only_a_Submitted_Release_can_be_withdrawn()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateCompleteDraftAsync(manager);

        using var withdrawn = await WithdrawAsync(manager, release);

        await AssertRefusedAsync(withdrawn, HttpStatusCode.Conflict, "Submitted");
    }

    [Fact]
    public async Task Only_managing_Members_submit_or_withdraw_and_the_Admin_is_not_one_of_them()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var stranger = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateCompleteDraftAsync(manager);

        using var submittedByStranger = await SubmitAsync(stranger, release);
        using var submittedByAdmin = await SubmitAsync(admin, release);
        using var submitted = await SubmitAsync(manager, release);
        using var withdrawnByStranger = await WithdrawAsync(stranger, release);
        using var withdrawnByAdmin = await WithdrawAsync(admin, release);

        Assert.Equal(HttpStatusCode.NotFound, submittedByStranger.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, submittedByAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, withdrawnByStranger.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, withdrawnByAdmin.StatusCode);
        Assert.Equal(ReleaseStage.Submitted, (await ReadAsync(manager, release)).Stage);
    }

    // Every way a managing Member can change a Release, so the Admin reviews exactly what was submitted.
    public static TheoryData<string> LockedChanges =>
    [
        "edit details", "delete", "cover link", "cover confirm", "add Track", "confirm Track",
        "replace Track", "edit Track", "reorder Tracks", "remove Track",
    ];

    [Theory]
    [MemberData(nameof(LockedChanges))]
    public async Task A_Submitted_Release_is_locked(string change)
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateCompleteDraftAsync(manager);
        var track = (await manager.GetFromJsonAsync<List<TrackSummary>>($"/api/tracks?releaseId={release.Id}"))!.Single();
        using var submitted = await SubmitAsync(manager, release);
        var root = $"/api/releases/{release.Id}";

        using var response = change switch
        {
            "edit details" => await manager.PutAsJsonAsync(root, CompleteForm("Stiekem Anders")),
            "delete" => await manager.DeleteAsync(root),
            "cover link" => await manager.PostAsync($"{root}/cover/upload-link", null),
            "cover confirm" => await manager.PostAsJsonAsync($"{root}/cover", new ConfirmUploadForm { UploadId = Guid.NewGuid() }),
            "add Track" => await manager.PostAsJsonAsync($"{root}/tracks", new TrackUploadForm { FileName = "extra.flac", Size = 1000 }),
            "confirm Track" => await manager.PostAsync($"{root}/tracks/{track.Id}/confirm", null),
            "replace Track" => await manager.PostAsJsonAsync($"{root}/tracks/{track.Id}/replace", new TrackUploadForm { FileName = "extra.flac", Size = 1000 }),
            "edit Track" => await manager.PutAsJsonAsync($"{root}/tracks/{track.Id}", new TrackForm { Title = "Stiekem" }),
            "reorder Tracks" => await manager.PutAsJsonAsync($"{root}/tracks/order", new TrackOrderForm { TrackIds = [track.Id] }),
            "remove Track" => await manager.DeleteAsync($"{root}/tracks/{track.Id}"),
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };

        // Confirming a Track that's already past Uploading is a harmless repeat: it changes nothing.
        Assert.Equal(change == "confirm Track" ? HttpStatusCode.OK : HttpStatusCode.Conflict, response.StatusCode);
        var read = await ReadAsync(manager, release);
        Assert.Equal(ReleaseStage.Submitted, read.Stage);
        Assert.Equal(release.Title, read.Title);
    }

    [Fact]
    public async Task The_Review_page_lists_Submitted_Releases_oldest_first_and_nothing_else()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var newer = await CreateCompleteDraftAsync(manager, "De Nieuwere");
        var older = await CreateCompleteDraftAsync(manager, "De Oudere");
        var withdrawn = await CreateCompleteDraftAsync(manager, "De Teruggetrokkene");
        var draft = await CreateCompleteDraftAsync(manager, "Het Ontwerp");

        var monday = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
        await app.SetClockAsync(monday.AddDays(1));
        using var newerSubmitted = await SubmitAsync(manager, newer);
        await app.SetClockAsync(monday);
        using var olderSubmitted = await SubmitAsync(manager, older);
        using var withdrawnSubmitted = await SubmitAsync(manager, withdrawn);
        using var taken = await WithdrawAsync(manager, withdrawn);
        await app.SetClockAsync(DateTimeOffset.UtcNow);

        var ours = (await ReadReviewAsync(admin)).Where(r => new[] { newer.Id, older.Id, withdrawn.Id, draft.Id }.Contains(r.Id)).ToList();

        Assert.Equal([older.Id, newer.Id], ours.Select(r => r.Id));
        Assert.Equal(monday, ours[0].SubmittedAt);
        Assert.Equal("De Inzenders", ours[0].ArtistName);
    }

    [Fact]
    public async Task Each_submission_records_its_Member_time_and_wording_version()
    {
        var firstName = $"Eerste {Guid.NewGuid():N}";
        using var manager = await app.LoginAsNewMemberAsync(firstName);
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateCompleteDraftAsync(manager);
        var first = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
        var second = first.AddHours(2);

        await app.SetClockAsync(first);
        using var submitted = await SubmitAsync(manager, release);
        using var withdrawn = await WithdrawAsync(manager, release);
        await app.SetClockAsync(second);
        using var resubmitted = await SubmitAsync(manager, release);
        await app.SetClockAsync(DateTimeOffset.UtcNow);

        var reviewed = Assert.Single(await ReadReviewAsync(admin), r => r.Id == release.Id);
        Assert.Equal(second, reviewed.SubmittedAt);
        Assert.Equal(
            [new MadeRightsDeclaration(firstName, second, 1), new MadeRightsDeclaration(firstName, first, 1)],
            reviewed.RightsDeclarations);
    }

    [Fact]
    public async Task The_Review_list_is_for_the_Admin_only()
    {
        using var member = await app.LoginAsNewMemberAsync();

        using var response = await member.GetAsync("/api/admin/review");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_badge_count_follows_Submit_and_Withdraw()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateCompleteDraftAsync(manager);
        var before = await ReadCountAsync(admin);

        using var submitted = await SubmitAsync(manager, release);
        Assert.Equal(before + 1, await ReadCountAsync(admin));

        using var withdrawn = await WithdrawAsync(manager, release);
        Assert.Equal(before, await ReadCountAsync(admin));
    }

    [Fact]
    public async Task Email_6_tells_the_Admin_what_was_submitted()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var title = $"Keuring {Guid.NewGuid():N}";
        var release = await CreateCompleteDraftAsync(manager, title);

        using var submitted = await SubmitAsync(manager, release);

        var email = await app.CreateMailPit().WaitForEmailAsync(KleinGeldApp.AdminEmail, title);
        Assert.Contains("De Inzenders", email.Text);
        Assert.Contains("Review page", email.Text);
    }

    [Fact]
    public async Task The_Admin_gets_no_email_about_their_own_Artist()
    {
        using var admin = await app.LoginAsAdminAsync();
        var title = $"Eigen Werk {Guid.NewGuid():N}";
        var release = await CreateCompleteDraftAsync(admin, title);

        using var submitted = await SubmitAsync(admin, release);

        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        await AssertNoEmailAsync(title);
    }

    [Fact]
    public async Task A_fellow_manager_submitting_for_an_Artist_the_Admin_manages_sends_no_email()
    {
        using var admin = await app.LoginAsAdminAsync();
        var fellowName = $"Bandlid {Guid.NewGuid():N}";
        using var fellow = await app.LoginAsNewMemberAsync(fellowName);
        var title = $"Samen Werk {Guid.NewGuid():N}";
        var release = await CreateCompleteDraftAsync(admin, title);
        var candidates = await admin.GetFromJsonAsync<List<ArtistManager>>($"/api/artists/{release.ArtistId}/managers/candidates");
        using var added = await admin.PostAsJsonAsync(
            $"/api/artists/{release.ArtistId}/managers",
            new AddManagerForm { MemberId = candidates!.Single(c => c.Name == fellowName).Id });
        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);

        using var submitted = await SubmitAsync(fellow, release);

        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        await AssertNoEmailAsync(title);
    }

    // The email goes out before Submit answers, so anything sent is already in MailPit.
    private async Task AssertNoEmailAsync(string title) =>
        Assert.Empty(await app.CreateMailPit().SearchAsync(KleinGeldApp.AdminEmail, title));

    [Fact]
    public async Task The_Admin_link_shows_the_count_on_server_rendered_pages_too()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateCompleteDraftAsync(manager);
        using var submitted = await SubmitAsync(manager, release);

        var page = await admin.GetStringAsync("/Account/Manage");
        var memberPage = await manager.GetStringAsync("/Account/Manage");

        Assert.Contains($"""<span class="review-badge" aria-label="Waiting for review">{await ReadCountAsync(admin)}</span>""", page);
        Assert.DoesNotContain("review-badge", memberPage);
    }

    private static async Task<int> ReadCountAsync(HttpClient admin) =>
        await admin.GetFromJsonAsync<int>("/api/admin/review/count");

    private static async Task<List<ReviewRelease>> ReadReviewAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<List<ReviewRelease>>("/api/admin/review"))!;

    private static Task<HttpResponseMessage> WithdrawAsync(HttpClient member, ReleaseDetails release) =>
        member.PostAsync($"/api/releases/{release.Id}/withdraw", null);

    private static ReleaseForm CompleteForm(string title = "Klaar Voor De Keuring") => new()
    {
        Title = title,
        Type = ReleaseType.Single,
        ReleaseDate = new DateOnly(2026, 1, 1),
    };

    private static async Task AssertRefusedAsync(HttpResponseMessage response, HttpStatusCode status, string reason)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Contains(reason, await response.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient member, ReleaseDetails release, bool rightsDeclared = true) =>
        member.PostAsJsonAsync($"/api/releases/{release.Id}/submit", new SubmitForm { RightsDeclared = rightsDeclared });

    private static async Task<ReleaseDetails> ReadAsync(HttpClient member, ReleaseDetails release) =>
        (await member.GetFromJsonAsync<ReleaseDetails>($"/api/releases/{release.Id}/{release.Slug}"))!;

    private static async Task<ReleaseDetails> CreateCompleteDraftAsync(HttpClient manager, string title = "Klaar Voor De Keuring")
    {
        var release = await CreateDraftAsync(manager, CompleteForm(title));
        await SetCoverAsync(manager, release);
        var track = await AddTrackAsync(manager, release, "tone.flac");
        await WaitUntilConvertedAsync(manager, release, track);
        return release;
    }

    private static async Task<ReleaseDetails> CreateDraftAsync(HttpClient manager, ReleaseForm form)
    {
        using var artistResponse = await manager.PostAsJsonAsync("/api/artists", new ArtistForm { Name = "De Inzenders" });
        var artist = await artistResponse.Content.ReadFromJsonAsync<ArtistSummary>();
        using var response = await manager.PostAsJsonAsync($"/api/artists/{artist!.Id}/releases", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReleaseDetails>())!;
    }

    private static async Task SetCoverAsync(HttpClient manager, ReleaseDetails release)
    {
        using var linkResponse = await manager.PostAsync($"/api/releases/{release.Id}/cover/upload-link", null);
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);
        var link = (await linkResponse.Content.ReadFromJsonAsync<UploadLink>())!;
        await PutBlobAsync(link.Url, SquarePng());
        using var confirmed = await manager.PostAsJsonAsync($"/api/releases/{release.Id}/cover", new ConfirmUploadForm { UploadId = link.UploadId });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    private static async Task<TrackUpload> AddTrackAsync(HttpClient manager, ReleaseDetails release, string fixture)
    {
        var file = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        using var response = await manager.PostAsJsonAsync($"/api/releases/{release.Id}/tracks", new TrackUploadForm { FileName = fixture, Size = file.Length });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var upload = (await response.Content.ReadFromJsonAsync<TrackUpload>())!;
        await PutBlobAsync(upload.UploadLink, file);
        using var confirmed = await manager.PostAsync($"/api/releases/{release.Id}/tracks/{upload.Track.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        return upload;
    }

    private static async Task<TrackStatus> WaitUntilConvertedAsync(HttpClient manager, ReleaseDetails release, TrackUpload upload)
    {
        using var timeout = new CancellationTokenSource(ConversionTimeout);
        while (true)
        {
            var tracks = await manager.GetFromJsonAsync<List<TrackSummary>>($"/api/tracks?releaseId={release.Id}");
            var status = tracks!.Single(t => t.Id == upload.Track.Id).Status;
            if (status is TrackStatus.Ready or TrackStatus.Failed)
            {
                return status;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(200), timeout.Token);
        }
    }

    private static async Task PutBlobAsync(Uri link, byte[] file)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, link) { Content = new ByteArrayContent(file) };
        request.Headers.Add("x-ms-blob-type", "BlockBlob");
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await BlobStorage.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static byte[] SquarePng()
    {
        using var bitmap = new SKBitmap(1000, 1000);
        bitmap.Erase(SKColors.Teal);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 90);
        return encoded.ToArray();
    }
}
