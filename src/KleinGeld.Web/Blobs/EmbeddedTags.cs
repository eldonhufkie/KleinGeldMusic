using Azure.Storage.Blobs;
using KleinGeld.Contracts;
using KleinGeld.Web.Api;

namespace KleinGeld.Web.Blobs;

/// <summary>What an audio file says about itself. Blank tags are null.</summary>
internal sealed record EmbeddedTags(string? Title)
{
    public static readonly EmbeddedTags None = new((string?)null);
}

/// <summary>
/// Reads the tags of an uploaded audio file with TagLib#, over a stream that fetches only the parts it needs. Tags
/// are a convenience, so a file whose tags can't be read still counts as audio and simply has none.
/// </summary>
internal sealed class TagReader(BlobLinks links, ILogger<TagReader> logger)
{
    /// <param name="blobName">The file in the audio container. Its name has no extension to go by.</param>
    /// <param name="format">The format ffprobe reported, which tells TagLib# how to read the file.</param>
    public async Task<EmbeddedTags> ReadAsync(string blobName, string format)
    {
        try
        {
            await using var stream = await links.Blob(TrackEndpoints.AudioContainer, blobName).OpenReadAsync();
            using var file = TagLib.File.Create(new BlobFile(blobName, stream), MimeTypeOf(format), TagLib.ReadStyle.None);
            return new EmbeddedTags(Clean(file.Tag.Title, ReleaseForm.TitleMaxLength));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // TagLib# throws many kinds of exception on damaged tags, and none of them should cost the Member the upload.
            logger.LogWarning(e, "The tags of {BlobName} couldn't be read.", blobName);
            return EmbeddedTags.None;
        }
    }

    private static string MimeTypeOf(string format) => format switch
    {
        "flac" => "taglib/flac",
        "wav" => "taglib/wav",
        "aiff" => "taglib/aiff",
        "mp3" => "taglib/mp3",
        "aac" => "taglib/aac",
        _ => "taglib/m4a",
    };

    private static string? Clean(string? text, int maxLength)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private sealed class BlobFile(string name, Stream stream) : TagLib.File.IFileAbstraction
    {
        public string Name => name;

        public Stream ReadStream => stream;

        public Stream WriteStream => stream;

        // The caller owns the stream.
        public void CloseStream(Stream closing)
        {
        }
    }
}
