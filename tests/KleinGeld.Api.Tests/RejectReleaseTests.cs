using System.Net.Http.Headers;
using System.Net.Http.Json;
using KleinGeld.Contracts;
using SkiaSharp;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class RejectReleaseTests(KleinGeldApp app) : IAsyncLifetime
{
    private static readonly HttpClient BlobStorage = new();
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromMinutes(1);

    // Earlier tests leave the clock anywhere; storage checks links against the real time.
    public Task InitializeAsync() => app.SetClockAsync(DateTimeOffset.UtcNow);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Rejecting_returns_the_Release_with_its_reason_on_My_Artists()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateSubmittedReleaseAsync(manager);

        using var rejected = await RejectAsync(admin, release, "The cover is blurry.");

        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var listed = await ReadOnMyArtistsAsync(manager, release);
        Assert.Equal(ReleaseStage.Rejected, listed.Stage);
        Assert.Equal("The cover is blurry.", listed.RejectionReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejecting_without_a_reason_is_refused(string reason)
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateSubmittedReleaseAsync(manager);

        using var rejected = await RejectAsync(admin, release, reason);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(ReleaseStage.Submitted, (await ReadAsync(manager, release)).Stage);
    }

    [Fact]
    public async Task A_reason_over_the_limit_is_refused()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateSubmittedReleaseAsync(manager);

        using var rejected = await RejectAsync(admin, release, new string('x', RejectForm.ReasonMaxLength + 1));

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    [Fact]
    public async Task Only_the_Admin_can_reject()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        var release = await CreateSubmittedReleaseAsync(manager);

        using var rejected = await RejectAsync(manager, release, "Mijn eigen oordeel.");

        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal(ReleaseStage.Submitted, (await ReadAsync(manager, release)).Stage);
    }

    [Fact]
    public async Task Only_a_Submitted_Release_can_be_rejected()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateSubmittedReleaseAsync(manager);
        using var withdrawn = await manager.PostAsync($"/api/releases/{release.Id}/withdraw", null);

        using var rejected = await RejectAsync(admin, release, "Te laat.");

        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal(ReleaseStage.Draft, (await ReadAsync(manager, release)).Stage);
    }

    [Fact]
    public async Task Rejecting_a_Release_nobody_has_is_not_found()
    {
        using var admin = await app.LoginAsAdminAsync();

        using var rejected = await admin.PostAsJsonAsync($"/api/admin/releases/{Guid.NewGuid()}/reject", new RejectForm { Reason = "Bestaat niet." });

        Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
    }

    [Fact]
    public async Task Every_managing_Member_gets_email_8_with_the_reason()
    {
        var firstEmail = $"{Guid.NewGuid():N}@example.test";
        var secondEmail = $"{Guid.NewGuid():N}@example.test";
        var secondName = $"Bandlid {Guid.NewGuid():N}";
        using var first = await app.LoginAsNewMemberAsync(email: firstEmail);
        using var second = await app.LoginAsNewMemberAsync(secondName, secondEmail);
        using var admin = await app.LoginAsAdminAsync();
        var title = $"Afgekeurd {Guid.NewGuid():N}";
        var release = await CreateSubmittedReleaseAsync(first, title);
        await AddManagerAsync(first, release, secondName);

        using var rejected = await RejectAsync(admin, release, "The second Track clips.");

        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var mailPit = app.CreateMailPit();
        foreach (var email in new[] { firstEmail, secondEmail })
        {
            var sent = await mailPit.WaitForEmailAsync(email, title);
            Assert.Contains("The second Track clips.", sent.Text);
        }
    }

    [Fact]
    public async Task The_Admin_gets_no_email_8_for_their_own_Artist_but_a_fellow_manager_does()
    {
        var fellowEmail = $"{Guid.NewGuid():N}@example.test";
        var fellowName = $"Bandlid {Guid.NewGuid():N}";
        using var admin = await app.LoginAsAdminAsync();
        using var fellow = await app.LoginAsNewMemberAsync(fellowName, fellowEmail);
        var title = $"Eigen Werk {Guid.NewGuid():N}";
        var release = await CreateSubmittedReleaseAsync(admin, title);
        await AddManagerAsync(admin, release, fellowName);

        using var rejected = await RejectAsync(admin, release, "Toch niet goed genoeg.");

        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var mailPit = app.CreateMailPit();
        await mailPit.WaitForEmailAsync(fellowEmail, title);
        // The email goes out before Reject answers, so anything sent is already in MailPit.
        Assert.Empty(await mailPit.SearchAsync(KleinGeldApp.AdminEmail, title));
    }

    [Fact]
    public async Task A_Rejected_Release_can_be_edited_and_stays_Rejected_with_its_reason()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateSubmittedReleaseAsync(manager);
        using var rejected = await RejectAsync(admin, release, "Wrong title.");

        using var edited = await manager.PutAsJsonAsync($"/api/releases/{release.Id}", new ReleaseForm
        {
            Title = "Betere Titel",
            Type = ReleaseType.EP,
            ReleaseDate = new DateOnly(2026, 2, 2),
        });

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var details = (await edited.Content.ReadFromJsonAsync<ReleaseDetails>())!;
        Assert.Equal("Betere Titel", details.Title);
        Assert.Equal(ReleaseStage.Rejected, details.Stage);
        Assert.Equal("Wrong title.", details.RejectionReason);
    }

    [Fact]
    public async Task A_Rejected_Release_takes_a_new_cover_and_Track_and_stays_Rejected()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateSubmittedReleaseAsync(manager);
        using var rejected = await RejectAsync(admin, release, "Needs another Track.");

        await SetCoverAsync(manager, release);
        await WaitUntilReadyAsync(manager, release, await AddTrackAsync(manager, release));

        var tracks = await manager.GetFromJsonAsync<List<TrackSummary>>($"/api/tracks?releaseId={release.Id}");
        Assert.Equal(2, tracks!.Count);
        Assert.Equal(ReleaseStage.Rejected, (await ReadAsync(manager, release)).Stage);
    }

    [Fact]
    public async Task A_Rejected_Release_cannot_be_withdrawn()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateSubmittedReleaseAsync(manager);
        using var rejected = await RejectAsync(admin, release, "Nee.");

        using var withdrawn = await manager.PostAsync($"/api/releases/{release.Id}/withdraw", null);

        Assert.Equal(HttpStatusCode.Conflict, withdrawn.StatusCode);
        Assert.Equal(ReleaseStage.Rejected, (await ReadAsync(manager, release)).Stage);
    }

    [Fact]
    public async Task Other_Members_cannot_see_a_Rejected_Release()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var other = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateSubmittedReleaseAsync(manager);
        using var rejected = await RejectAsync(admin, release, "Privé.");

        using var read = await other.GetAsync($"/api/releases/{release.Id}/{release.Slug}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task Resubmitting_without_a_new_Rights_Declaration_is_refused()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateSubmittedReleaseAsync(manager);
        using var rejected = await RejectAsync(admin, release, "Opnieuw graag.");

        using var resubmitted = await SubmitAsync(manager, release, rightsDeclared: false);

        Assert.Equal(HttpStatusCode.BadRequest, resubmitted.StatusCode);
        var details = await ReadAsync(manager, release);
        Assert.Equal(ReleaseStage.Rejected, details.Stage);
        Assert.Equal("Opnieuw graag.", details.RejectionReason);
    }

    [Fact]
    public async Task Resubmitting_returns_the_Release_to_review_with_a_new_Rights_Declaration_and_no_reason()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateSubmittedReleaseAsync(manager);
        using var rejected = await RejectAsync(admin, release, "Opnieuw graag.");

        using var resubmitted = await SubmitAsync(manager, release);

        Assert.Equal(HttpStatusCode.OK, resubmitted.StatusCode);
        var listed = await ReadOnMyArtistsAsync(manager, release);
        Assert.Equal(ReleaseStage.Submitted, listed.Stage);
        Assert.Null(listed.RejectionReason);
        var review = await admin.GetFromJsonAsync<List<ReviewRelease>>("/api/admin/review");
        Assert.Equal(2, review!.Single(r => r.Id == release.Id).RightsDeclarations.Count);
    }

    [Fact]
    public async Task A_Rejected_Release_leaves_the_Review_page()
    {
        using var manager = await app.LoginAsNewMemberAsync();
        using var admin = await app.LoginAsAdminAsync();
        var release = await CreateSubmittedReleaseAsync(manager);

        using var rejected = await RejectAsync(admin, release, "Weg ermee.");

        var review = await admin.GetFromJsonAsync<List<ReviewRelease>>("/api/admin/review");
        Assert.DoesNotContain(review!, r => r.Id == release.Id);
    }

    private static async Task AddManagerAsync(HttpClient manager, ReleaseDetails release, string name)
    {
        var candidates = await manager.GetFromJsonAsync<List<ArtistManager>>($"/api/artists/{release.ArtistId}/managers/candidates");
        using var added = await manager.PostAsJsonAsync(
            $"/api/artists/{release.ArtistId}/managers",
            new AddManagerForm { MemberId = candidates!.Single(c => c.Name == name).Id });
        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
    }

    private static Task<HttpResponseMessage> RejectAsync(HttpClient admin, ReleaseDetails release, string reason) =>
        admin.PostAsJsonAsync($"/api/admin/releases/{release.Id}/reject", new RejectForm { Reason = reason });

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient member, ReleaseDetails release, bool rightsDeclared = true) =>
        member.PostAsJsonAsync($"/api/releases/{release.Id}/submit", new SubmitForm { RightsDeclared = rightsDeclared });

    private static async Task<ReleaseDetails> ReadAsync(HttpClient member, ReleaseDetails release) =>
        (await member.GetFromJsonAsync<ReleaseDetails>($"/api/releases/{release.Id}/{release.Slug}"))!;

    private static async Task<ManagedRelease> ReadOnMyArtistsAsync(HttpClient manager, ReleaseDetails release)
    {
        var artist = await manager.GetFromJsonAsync<ManagedArtist>($"/api/my-artists/{release.ArtistId}");
        return artist!.Releases.Single(r => r.Id == release.Id);
    }

    private static async Task<ReleaseDetails> CreateSubmittedReleaseAsync(HttpClient manager, string title = "Terug Naar De Tekentafel")
    {
        using var artistResponse = await manager.PostAsJsonAsync("/api/artists", new ArtistForm { Name = "De Afgewezenen" });
        var artist = await artistResponse.Content.ReadFromJsonAsync<ArtistSummary>();
        using var response = await manager.PostAsJsonAsync($"/api/artists/{artist!.Id}/releases", new ReleaseForm
        {
            Title = title,
            Type = ReleaseType.Single,
            ReleaseDate = new DateOnly(2026, 1, 1),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var release = (await response.Content.ReadFromJsonAsync<ReleaseDetails>())!;

        await SetCoverAsync(manager, release);
        await WaitUntilReadyAsync(manager, release, await AddTrackAsync(manager, release));
        using var submitted = await SubmitAsync(manager, release);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        return release;
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

    private static async Task<TrackUpload> AddTrackAsync(HttpClient manager, ReleaseDetails release)
    {
        var file = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tone.flac"));
        using var response = await manager.PostAsJsonAsync($"/api/releases/{release.Id}/tracks", new TrackUploadForm { FileName = "tone.flac", Size = file.Length });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var upload = (await response.Content.ReadFromJsonAsync<TrackUpload>())!;
        await PutBlobAsync(upload.UploadLink, file);
        using var confirmed = await manager.PostAsync($"/api/releases/{release.Id}/tracks/{upload.Track.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        return upload;
    }

    private static async Task WaitUntilReadyAsync(HttpClient manager, ReleaseDetails release, TrackUpload upload)
    {
        using var timeout = new CancellationTokenSource(ConversionTimeout);
        while (true)
        {
            var tracks = await manager.GetFromJsonAsync<List<TrackSummary>>($"/api/tracks?releaseId={release.Id}");
            if (tracks!.Single(t => t.Id == upload.Track.Id).Status == TrackStatus.Ready)
            {
                return;
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
