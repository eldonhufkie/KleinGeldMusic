using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using KleinGeld.Contracts;
using SkiaSharp;

namespace KleinGeld.Web.Blobs;

/// <summary>
/// The cover art of Releases: <c>{releaseId}/{coverId}/original</c> as uploaded, beside its JPEG copies. Every upload
/// gets a new cover id, so replacing a cover writes new blobs and never touches the old ones.
/// </summary>
internal sealed class CoverStore(BlobServiceClient blobs, BlobLinks links)
{
    public const string Container = "covers";
    private const int MinimumSide = 1000;
    private const long MaximumBytes = 20L * 1024 * 1024;
    private const int SmallSide = 300;
    private const int LargeSide = 1000;

    /// <summary>Not a rule Members meet: it stops a small file from decoding into gigabytes of pixels.</summary>
    private const int MaximumSide = 10_000;

    private static readonly TimeSpan ReadLinkLifetime = TimeSpan.FromHours(1);

    private BlobContainerClient Covers => blobs.GetBlobContainerClient(Container);

    public Task<UploadLink> CreateUploadLinkAsync(Guid releaseId)
    {
        var coverId = Guid.CreateVersion7();
        return links.ForUploadAsync(Container, Original(releaseId, coverId), coverId);
    }

    /// <summary>
    /// Checks an uploaded cover and writes its copies. Answers why the cover was refused, or null when it's accepted.
    /// A refused upload is deleted at once.
    /// </summary>
    public async Task<string?> AcceptAsync(Guid releaseId, Guid coverId)
    {
        var original = Covers.GetBlobClient(Original(releaseId, coverId));

        long size;
        try
        {
            size = (await original.GetPropertiesAsync()).Value.ContentLength;
        }
        catch (RequestFailedException e) when (e.Status == StatusCodes.Status404NotFound)
        {
            return "The upload didn't arrive. Try uploading the cover again.";
        }

        var refusal = size > MaximumBytes ? "The cover is larger than 20 MB." : null;
        if (refusal is null)
        {
            var file = (await original.DownloadContentAsync()).Value.Content.ToArray();
            using var data = SKData.CreateCopy(file);
            using var codec = SKCodec.Create(data);
            refusal = Check(codec);
            if (refusal is null)
            {
                using var image = SKBitmap.Decode(codec);
                if (image is null)
                {
                    refusal = "The cover file is damaged.";
                }
                else
                {
                    await WriteCopyAsync(image, releaseId, coverId, SmallSide);
                    await WriteCopyAsync(image, releaseId, coverId, LargeSide);
                    await original.SetHttpHeadersAsync(new BlobHttpHeaders
                    {
                        ContentType = codec.EncodedFormat == SKEncodedImageFormat.Png ? "image/png" : "image/jpeg",
                    });
                }
            }
        }

        if (refusal is not null)
        {
            await original.DeleteIfExistsAsync();
        }
        return refusal;
    }

    public CoverArt LinksTo(Guid releaseId, Guid coverId) => new(
        links.ForReading(Container, Copy(releaseId, coverId, SmallSide), ReadLinkLifetime),
        links.ForReading(Container, Copy(releaseId, coverId, LargeSide), ReadLinkLifetime));

    /// <summary>Deletes every cover a Release ever had, and any upload nobody confirmed.</summary>
    public async Task DeleteAllAsync(Guid releaseId)
    {
        if (!await Covers.ExistsAsync())
        {
            return;
        }

        await foreach (var blob in Covers.GetBlobsAsync(new GetBlobsOptions { Prefix = $"{releaseId}/" }))
        {
            await Covers.DeleteBlobIfExistsAsync(blob.Name);
        }
    }

    private static string? Check(SKCodec? codec)
    {
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png))
        {
            return "The cover must be a JPEG or PNG image.";
        }

        var (width, height) = (codec.Info.Width, codec.Info.Height);
        if (width != height)
        {
            return $"The cover must be square; this one is {width}×{height} px.";
        }
        if (width < MinimumSide)
        {
            return $"The cover must be at least {MinimumSide}×{MinimumSide} px; this one is {width}×{height} px.";
        }
        return width > MaximumSide
            ? $"The cover must be at most {MaximumSide}×{MaximumSide} px; this one is {width}×{height} px."
            : null;
    }

    private async Task WriteCopyAsync(SKBitmap image, Guid releaseId, Guid coverId, int side)
    {
        // A PNG may be see-through and a JPEG can't be, so copies sit on white.
        using var copy = new SKBitmap(side, side, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(copy))
        using (var source = SKImage.FromBitmap(image))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawImage(source, new SKRect(0, 0, side, side), new SKSamplingOptions(SKCubicResampler.Mitchell));
        }
        using var jpeg = copy.Encode(SKEncodedImageFormat.Jpeg, 85);

        try
        {
            await Covers.GetBlobClient(Copy(releaseId, coverId, side)).UploadAsync(jpeg.AsStream(), new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            });
        }
        catch (RequestFailedException e) when (e.Status == StatusCodes.Status409Conflict)
        {
            // A repeated confirmation: the copy is already there, made from the same original.
        }
    }

    private static string Original(Guid releaseId, Guid coverId) => $"{releaseId}/{coverId}/original";

    private static string Copy(Guid releaseId, Guid coverId, int side) => $"{releaseId}/{coverId}/{side}.jpg";
}
