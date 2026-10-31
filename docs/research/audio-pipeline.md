# Research: audio pipeline (formats, conversion, streaming)

Answers ticket `03-audio-pipeline` ("How should audio go from an Artist's upload to a Member's browser player?"). Researched 2026-10-31 against primary sources; every claim links to its source. Lines marked **Judgement** are recommendations, not sourced facts.

## Recommended pipeline

| Step | Recommendation |
|---|---|
| **Accept** | Lossless uploads preferred: **FLAC, WAV, AIFF, ALAC (.m4a)**. Also accept lossy **MP3 / AAC (.m4a)** with a "lossless is better" hint. Reject everything else after probing the file with ffprobe. Raise Kestrel's 28.6 MB request body limit for the upload endpoint. |
| **Convert** | Keep the original. A background worker runs FFmpeg once per Track and produces one streaming file: **AAC-LC, 44.1/48 kHz stereo, ~192 kbps CBR, in an MP4 (.m4a) container with `+faststart`**. Use FFmpeg's built-in `aac` encoder. Call it through **FFMpegCore** (MIT) or plain `Process`/CliWrap. |
| **Stream** | Plain **HTTP progressive download with byte-range support** for the single `.m4a`. No HLS/DASH in v1. Either ASP.NET Core `TypedResults.File(stream, "audio/mp4", enableRangeProcessing: true)` over a seekable stream, or a direct object-storage URL (Azure Blob supports `Range` natively). |
| **Protect** | **Default: proxy through the API.** The API checks the Member's session cookie on each request, then streams the bytes with range processing. Use this while ticket 07 (storage) and ticket 04 (identity) are undecided, because it works with local disk and with blobs. **If storage is Azure Blob and egress through the API is a problem:** an authenticated API endpoint returns a short-lived (minutes), read-only, single-blob **user delegation SAS** URL. |
| **Metadata** | **TagLib#** (LGPL-2.1) for embedded title/artist/track number/cover art (`Tag.Pictures`) and duration. Or **ffprobe** via FFMpegCore, which can report duration of anything FFmpeg can decode. Treat embedded tags only as pre-fill for the Artist's form. |

At this scale (~50 Members, one file per Track), HLS adds a segmenter, playlists, hls.js on non-Safari browsers and per-segment authorisation. Its main benefit is adaptive bitrate, which a single ~192 kbps audio file doesn't need. See [Streaming method](#4-streaming-method-range-requests-vs-hlsdash).

---

## 1. Upload formats

**What independent musicians have.** Musicians export a master from a DAW, so they usually have a lossless file. Bandcamp, the most common self-release platform, asks Artists for lossless files: "WAV, AIFF and FLAC are high-fidelity, lossless formats". It says it needs "the highest possible quality source" to create its streaming and download versions. On MP3 uploads: "We strongly recommend against it" ([Bandcamp Help: How and why should I upload lossless files](https://get.bandcamp.help/en/articles/15263231-how-and-why-should-i-upload-lossless-files)).

**What our tooling can read.** TagLib# reads tags from aac, aiff, flac, m4a, mp3, ogg/oga, wav, wma, webm and others ([TagLib# README](https://github.com/mono/taglib-sharp/blob/main/README.md)). FFmpeg decodes all of these, so ffprobe can be the gatekeeper ([FFMpegCore README: FFProbe.AnalyseAsync](https://github.com/rosenbjerg/FFMpegCore/blob/main/README.md)).

**Size.** Kestrel's default maximum request body is "30,000,000 bytes, which is approximately 28.6 MB". It can be raised per endpoint (`RequestSizeLimit` / `IHttpMaxRequestBodySizeFeature`) or globally (`KestrelServerLimits.MaxRequestBodySize`) ([Kestrel options](https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/fundamentals/servers/kestrel/options.md)). Uncompressed 16-bit/44.1 kHz stereo WAV is 1,411 kbit/s, about 10.6 MB per minute (arithmetic: 44,100 × 16 × 2), so a 5-minute WAV is about 53 MB and goes over the default.

**Judgement.** Accept an allowlist checked by ffprobe (codec, not file extension): FLAC, PCM WAV, AIFF, ALAC, MP3, AAC. Store the original unchanged, so we can re-encode later (other bitrate, Opus, downloads) without asking Artists to upload again. Skip Ogg/Opus/WMA uploads for v1 unless an Artist asks; it costs nothing to add them later because FFmpeg decodes them.

## 2. Conversion

**Convert to one streaming format, keep the original. Judgement, based on the browser matrix in §3.** One streaming rendition is enough: AAC-LC in MP4. Reasons:

- MDN: "AAC in an MP4 container is supported by all major browsers, which makes this a great choice… AAC is likely your best choice if you can only support one audio format" ([MDN: Web audio codec guide](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Audio_codecs)).
- Apple lists "AAC-LC audio, up to 48 kHz" as supported on iOS ([Apple: Safari Web Content Guide, Creating Video](https://developer.apple.com/library/archive/documentation/AppleApplications/Reference/SafariWebContent/CreatingVideoforSafarioniPhone/CreatingVideoforSafarioniPhone.html)). Resample 88.2/96 kHz masters down to 44.1/48 kHz.
- Streaming lossless originals would be large, and FLAC "is not supported at all on iOS" according to MDN's container guide ([MDN: Media container formats](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Containers)). The MDN codec table lists Safari 11 for FLAC, so the two MDN pages disagree. Either way, it's an unnecessary risk.

**Encoder.** FFmpeg's built-in `aac` encoder is "the default AAC encoder, natively implemented into FFmpeg". Setting a bitrate (`-b:a`) "automatically activates constant bit rate (CBR) mode", and the default is 128 kbps ([FFmpeg codecs: aac](https://ffmpeg.org/ffmpeg-codecs.html#aac)). The higher-quality `libfdk_aac` "is also incompatible with GPL" and needs an `--enable-nonfree` build ([FFmpeg codecs: libfdk_aac](https://ffmpeg.org/ffmpeg-codecs.html#libfdk_005faac)). Stock FFmpeg packages don't include it, so use the native encoder. FFmpeg's base licence is LGPL 2.1, and the GPL applies if GPL parts are compiled in ([FFmpeg legal](https://ffmpeg.org/legal.html)). Running a stock `ffmpeg` binary as a separate process on our own server isn't distributing it. **Judgement:** pick ~192 kbps CBR as a safe music bitrate above the 128 kbps default. It can be changed later because originals are kept.

**`+faststart`.** The MP4 muxer normally writes its index (`moov` atom) at the end. `-movflags +faststart` does "a second pass moving the index (moov atom) to the beginning of the file" ([FFmpeg formats: mov/mp4 muxer](https://ffmpeg.org/ffmpeg-formats.html#mov_002c-mp4_002c-ismv)). Without it, the browser has to fetch the end of the file before it can start playback.

Command, run per Track:

```
ffmpeg -i <original> -vn -map_metadata -1 -c:a aac -b:a 192k -ar 44100 -ac 2 -movflags +faststart <track>.m4a
```

(`-vn` drops embedded cover-art streams. We serve cover art separately as the Release's cover art.)

**.NET tooling.**

| Library | Licence | Latest NuGet | Notes |
|---|---|---|---|
| [FFMpegCore](https://github.com/rosenbjerg/FFMpegCore) | MIT | 5.5.0 | "A .NET Standard FFMpeg/FFProbe wrapper". Fluent argument builder (`WithAudioCodec(AudioCodec.Aac)`, `WithFastStart()`), `FFProbe.AnalyseAsync`. Needs `ffmpeg`/`ffprobe` binaries on PATH or in a configured `BinaryFolder` ([README](https://github.com/rosenbjerg/FFMpegCore/blob/main/README.md)). |
| [CliWrap](https://github.com/Tyrrrz/CliWrap) | MIT | 3.10.5 | General process launcher: stream redirection, cancellation ([README](https://github.com/Tyrrrz/CliWrap/blob/prime/Readme.md)). Its README adds political "Terms of use" on top of the MIT licence. Weigh that, or just use `System.Diagnostics.Process`. |
| [TagLib#](https://github.com/mono/taglib-sharp) (`TagLibSharp`) | LGPL-2.1 | 2.3.0 | Metadata only (see §6). Last pushed 2025-05, so slow-moving but API-stable ("only API additions… in the 2.0 series") ([README](https://github.com/mono/taglib-sharp/blob/main/README.md)). |

(Licences and versions from the GitHub API and NuGet flat-container index, queried 2026-10-31.)

**Where it runs.** Use a queued `BackgroundService` in the API, or a separate Aspire worker project. It takes "Track uploaded" jobs and runs FFmpeg. Mark the Track ready when the file is written ([ASP.NET Core: Background tasks with hosted services, queued background tasks](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)). The FFmpeg binary has to be in the container image or on the host. FFMpegCore's optional downloader fetches it from ffbinaries at runtime ([README](https://github.com/rosenbjerg/FFMpegCore/blob/main/README.md)). Prefer baking a pinned binary into the image.

**How long per Track. Not measured: no FFmpeg on the research machine, and no primary source gives a figure.** Audio-only AAC encoding is a single-threaded CPU job, and FFmpeg's `speed=` output usually shows it running many times faster than real time. Expect seconds per Track, not minutes. **To do:** benchmark one 5-minute 24-bit WAV on the chosen host before committing to inline (synchronous) conversion. A background queue avoids the question either way.

## 3. Browser support (`<audio>`)

| Format | Chrome | Firefox | Safari macOS | Safari iOS | Source |
|---|---|---|---|---|---|
| **AAC in MP4 (.m4a)** | Yes | Yes, via the OS decoder ("Firefox relies upon a platform's native support for AAC") | Yes | Yes (AAC-LC ≤ 48 kHz) | [MDN codecs](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Audio_codecs), [Apple](https://developer.apple.com/library/archive/documentation/AppleApplications/Reference/SafariWebContent/CreatingVideoforSafarioniPhone/CreatingVideoforSafarioniPhone.html) |
| **MP3** | Yes | Yes (built-in since 71) | Yes (3.1+) | Yes | [MDN codecs](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Audio_codecs) |
| **Opus** | Yes (33+) | Yes (15+) | MP4/WebM stereo-only from Safari 17 on macOS Sonoma; Ogg from macOS 15.4 | Ogg from iOS 18.4; older iOS only in CAF | [MDN codecs](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Audio_codecs), [Safari 17 release notes](https://developer.apple.com/documentation/safari-release-notes/safari-17-release-notes) ("Added support for stereo-only Opus in MPEG-4 and WebM containers on macOS Sonoma"), [WebKit: Safari 18.4](https://webkit.org/blog/16574/webkit-features-in-safari-18-4/) ("Ogg container support for both Opus and Vorbis audio on macOS Sequoia 15.4, iOS 18.4…") |
| **Vorbis (Ogg)** | Yes | Yes | macOS 15.4+ only | iOS 18.4+ only | same as above |
| **FLAC** | Yes | Yes (51 desktop / 58 mobile) | MDN tables disagree | MDN container guide: "not supported at all on iOS" | [MDN codecs](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Audio_codecs), [MDN containers](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Containers) |

Takeaways:

- AAC/MP4 and MP3 are the only formats that work on every target, including older iPhones. Opus is more efficient but only recently and partly supported on Apple devices. Ogg/Opus needs iOS 18.4+.
- Caveat: Firefox decodes AAC through the OS ([MDN](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Audio_codecs)). A Linux machine without system codecs may fail on AAC, while MP3 is decoded by Firefox itself since version 71. If a Member reports this, add an MP3 rendition as a second `<source>`. "The browser will then use the first one it understands", and the `type` attribute lets it skip unsupported files without fetching them ([MDN: `<audio>`](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/audio)).
- Use `preload="metadata"`. "The spec advises it to be set to `metadata`" ([MDN: `<audio>`](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/audio)). Seeking then works through range requests without downloading whole Tracks for a Playlist.

## 4. Streaming method: range requests vs HLS/DASH

**Range requests are required.** Apple: "HTTP servers hosting media files for iOS must support byte-range requests, which iOS uses to perform random access in media playback" ([Apple: Safari Web Content Guide](https://developer.apple.com/library/archive/documentation/AppleApplications/Reference/SafariWebContent/CreatingVideoforSafarioniPhone/CreatingVideoforSafarioniPhone.html)). The same page gives a test: `curl --range 0-99 <url> -o /dev/null` should download 100 bytes.

**Option A: through ASP.NET Core.** File results take `enableRangeProcessing: true`. The framework then returns `200` for a full request, "`206 Partial Content` with the `Content-Range` header" for `Range: bytes=…`, and `416 Range Not Satisfiable` for invalid ranges. Setting `entityTag`/`lastModified` also makes `If-Range` work ([ASP.NET Core: Minimal API responses, File result support for range requests](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0#file-result-support-for-range-requests)). `TypedResults.Stream(Stream, …, enableRangeProcessing)` "supports range requests" and "is an alias for File(Stream, …)". The stream "is disposed after the response is sent" ([API ref: TypedResults.Stream](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.typedresults.stream?view=aspnetcore-10.0)).
- **Gotcha:** the result only learns the length if the stream is seekable (`if (fileStream.CanSeek) FileLength = fileStream.Length;`), and range headers are only set when the length is known ([FileStreamHttpResult.cs](https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Results/src/FileStreamHttpResult.cs), [FileResultHelper.cs](https://github.com/dotnet/aspnetcore/blob/main/src/Shared/ResultsHelpers/FileResultHelper.cs)). A `FileStream` from local disk works. For Azure Blob, `BlobBaseClient.OpenReadAsync` returns a stream that "will only download the blob as the stream is read from" ([API ref](https://learn.microsoft.com/en-us/dotnet/api/azure.storage.blobs.specialized.blobbaseclient.openreadasync?view=azure-dotnet)), and its implementation reports `CanSeek => true` ([LazyLoadingReadOnlyStream.cs](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/storage/Azure.Storage.Common/src/Shared/LazyLoadingReadOnlyStream.cs)). So range proxying over blobs works without buffering whole files. Verify with the curl test above.
- **Option B: direct object-storage URL.** Azure Blob GET supports the standard `Range` header (`bytes=start-end`, and `bytes=start-` for service version 2011-08-18+), returning 206/416 ([Azure: Specifying the range header for Blob service operations](https://learn.microsoft.com/en-us/rest/api/storageservices/specifying-the-range-header-for-blob-service-operations)). The browser talks to storage directly, and the API only hands out URLs (see §5).

**HLS/DASH: what we'd gain and what it costs.**
- *Gain:* adaptive bitrate switching between renditions, plus the option to encrypt segments. At ~192 kbps a whole 4-minute Track is about 5.8 MB (arithmetic), so adaptive switching barely matters for audio.
- *Cost:* only Safari plays HLS natively: "Safari browsers (iOS, iPadOS, and macOS) have built-in HLS support". Chrome/Firefox need hls.js, which "relies on… MediaSource Extensions… and on Managed Media Source where MSE is not available (Safari on iOS 17.1+)". hls.js also warns that `canPlayType('application/vnd.apple.mpegurl')` returns "maybe" in Chrome even though "Chrome 147 reports support but may fail to play certain assets natively" ([hls.js README](https://github.com/video-dev/hls.js/blob/master/README.md)). HLS also means a segmenting step, a playlist plus many segment files per Track, and authorising every segment request (proxy or a signed URL per segment) instead of one file.
- **Judgement:** don't use HLS for v1. Revisit only if we add native apps or need protection beyond signed URLs.

## 5. Access control

Facts that shape the options:

- An `<audio src>` request is a plain browser fetch. JavaScript can't add an `Authorization: Bearer` header to it. Without a `crossorigin` attribute the resource "is fetched without a CORS request". With `crossorigin="use-credentials"` a cross-origin request carries "a cookie, a certificate, or… HTTP Basic authentication" ([MDN: `<audio>` crossorigin](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/audio)). So access control has to come from a **cookie** (proxy option) or **something in the URL** (signed-URL option). This links to ticket 04 (identity): cookie auth on the same origin as the API makes proxying trivial.
- Microsoft describes the same two designs: (1) "a front-end proxy service, which performs authentication", or (2) "A lightweight service authenticates the client as needed and then generates a SAS… the client application… can access storage account resources directly" ([Azure: SAS overview](https://learn.microsoft.com/en-us/azure/storage/common/storage-sas-overview)).

**Option 1: proxy through the API (recommended default).** `GET /tracks/{id}/audio` checks the Member's session and that the Track's Release is Published (or that the caller manages the Artist, or is the Admin). It then returns the stream with `enableRangeProcessing: true` (§4). A copied URL is useless without the Member's cookie. A Takedown takes effect immediately because every request is checked. The cost is that all audio bytes go through the API. At ~50 Members × ~6 MB per Track, that's small. **Judgement:** fine at this scale.

**Option 2: short-lived signed URL (Azure Blob SAS).** The API checks the Member's session, then returns a SAS URL to the `<audio>` element. Microsoft's guidance:
- "Microsoft recommends using a user delegation SAS… secured with Microsoft Entra credentials instead of the account key". Creating one needs the `generateUserDelegationKey` permission ([SAS overview](https://learn.microsoft.com/en-us/azure/storage/common/storage-sas-overview)).
- "If a SAS is leaked, it can be used by anyone who obtains it". Use "near-term expiration times", "Grant the least possible privileges… read access to that single object", "Always use HTTPS". Allow for clock skew: "set the start time to be at least 15 minutes in the past. Or, don't set it at all". The same skew can apply to expiry ([SAS overview](https://learn.microsoft.com/en-us/azure/storage/common/storage-sas-overview)).
- "The SAS token is not tracked by Azure Storage in any way" ([SAS overview](https://learn.microsoft.com/en-us/azure/storage/common/storage-sas-overview)). A copied URL works for anyone until it expires, and a Takedown doesn't revoke URLs already handed out.
- **Judgement:** if used, set expiry to about Track length plus a margin (e.g. 15-30 minutes). The player has to fetch a new URL when it resumes after a long pause, because range requests made after expiry return 403.

Neither option stops a Member who wants to save the audio: whatever the browser can play, it can save. Both stop link sharing with non-Members, which is the goal in the ticket.

## 6. Metadata

- **TagLib#:** `TagLib.File.Create(path)` exposes `Tag.Title` and `Properties.Duration` ([README](https://github.com/mono/taglib-sharp/blob/main/README.md)). Embedded cover art comes from `Tag.Pictures` (`IPicture[]`) ([Tag.cs](https://github.com/mono/taglib-sharp/blob/main/src/TaglibSharp/Tag.cs)). `File.Create(IFileAbstraction …)` reads from a non-file source such as an upload stream ([File.cs](https://github.com/mono/taglib-sharp/blob/main/src/TaglibSharp/File.cs)). Formats are listed in §1.
- **ffprobe via FFMpegCore:** `await FFProbe.AnalyseAsync(path)` returns media info, including duration and codec ([FFMpegCore README](https://github.com/rosenbjerg/FFMpegCore/blob/main/README.md)). This is the same probe we run anyway to validate uploads (§1), so it can be the **source of truth for duration and codec**. Use TagLib# only for tags and cover art. Alternatively, take the duration of the final `.m4a` once conversion finishes.
- **Judgement:** embedded tags only pre-fill the Track title / track number and suggest a Release cover art in the Artist's Draft form. The Artist confirms them. Strip tags from the streaming file (`-map_metadata -1` above) so what Members see comes only from our database.

## Open questions for other tickets

- **07 (hosting/storage):** local disk vs Blob affects only the storage behind §4/§5. Proxying works for both.
- **04 (identity):** cookie-based auth on the API's origin is assumed by the proxy option.
- Benchmark conversion time on the chosen host (§2).
