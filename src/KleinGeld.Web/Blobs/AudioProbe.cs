using System.Diagnostics;
using System.Text.Json;

namespace KleinGeld.Web.Blobs;

/// <summary>
/// Decides by ffprobe, never by file name, whether a file is audio in an accepted format: FLAC, WAV, AIFF, ALAC, MP3
/// or AAC. ffprobe reads only what it needs over the link, so a large file isn't downloaded. It takes the name in the
/// link as a hint and can exit cleanly for a file it can't read, so what counts is the stream it reports.
/// </summary>
internal sealed class AudioProbe(IConfiguration configuration)
{
    private const string Mp4Family = "mov,mp4,m4a,3gp,3g2,mj2";

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1);

    public async Task<bool> IsAcceptedAudioAsync(Uri link)
    {
        var start = new ProcessStartInfo(configuration["Ffprobe:Path"] ?? "ffprobe")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        // Quiet, because ffprobe's error messages would repeat the link and its signature.
        foreach (var argument in new[] { "-v", "quiet", "-print_format", "json", "-show_format", "-show_streams", link.AbsoluteUri })
        {
            start.ArgumentList.Add(argument);
        }

        // Failing to start throws: a missing ffprobe is a fault of this server, not a verdict on the file.
        using var process = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return process.ExitCode == 0 && IsAccepted(output);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            return false;
        }
    }

    private static bool IsAccepted(string ffprobeOutput)
    {
        using var report = JsonDocument.Parse(ffprobeOutput);
        if (!report.RootElement.TryGetProperty("streams", out var streams)
            || !report.RootElement.TryGetProperty("format", out var format))
        {
            return false;
        }

        var all = streams.EnumerateArray().ToList();
        var audio = all.Where(s => s.GetProperty("codec_type").GetString() == "audio").ToList();
        // Embedded cover art is a video stream marked as an attached picture; a real video isn't a Track.
        var hasVideo = all.Any(s => s.GetProperty("codec_type").GetString() == "video" && !IsAttachedPicture(s));

        return audio.Count == 1
            && !hasVideo
            && IsAcceptedCodec(format.GetProperty("format_name").GetString(), audio[0].GetProperty("codec_name").GetString());
    }

    private static bool IsAttachedPicture(JsonElement stream) =>
        stream.TryGetProperty("disposition", out var disposition)
        && disposition.TryGetProperty("attached_pic", out var attached)
        && attached.GetInt32() == 1;

    private static bool IsAcceptedCodec(string? format, string? codec) => format switch
    {
        "flac" => codec == "flac",
        "wav" or "aiff" => codec?.StartsWith("pcm_", StringComparison.Ordinal) == true,
        "mp3" => codec == "mp3",
        "aac" => codec == "aac",
        Mp4Family => codec is "aac" or "alac",
        _ => false,
    };
}
