using System.Diagnostics;
using System.Globalization;

namespace KleinGeld.Worker;

/// <summary>Turns an audio file into the fast-start 192 kbps AAC <c>.m4a</c> that Members stream.</summary>
internal sealed class AudioConverter(IConfiguration configuration)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Returns the converted file's duration, or null when the file can't be converted. FFmpeg can exit cleanly having
    /// decoded nothing, so what counts is whether the result has a duration. A missing FFmpeg throws: that is a fault
    /// of this server, not a verdict on the file.
    /// </summary>
    public async Task<TimeSpan?> ConvertAsync(string input, string output, CancellationToken cancellation)
    {
        var (converted, _) = await RunAsync(
            configuration["Ffmpeg:Path"] ?? "ffmpeg",
            ["-nostdin", "-v", "error", "-y", "-i", input, "-vn", "-map", "0:a:0",
             "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", output],
            cancellation);
        if (!converted)
        {
            return null;
        }

        var (probed, report) = await RunAsync(
            configuration["Ffprobe:Path"] ?? "ffprobe",
            ["-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", output],
            cancellation);
        return probed
            && double.TryParse(report.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : null;
    }

    private static async Task<(bool Succeeded, string Output)> RunAsync(
        string program, IEnumerable<string> arguments, CancellationToken cancellation)
    {
        var start = new ProcessStartInfo(program) { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(Timeout);
        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return (process.ExitCode == 0, output);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            // Shutting down says nothing about the file; the Track is picked up again on the next start.
            cancellation.ThrowIfCancellationRequested();
            return (false, "");
        }
    }
}
