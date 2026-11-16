using System.ComponentModel.DataAnnotations;

namespace KleinGeld.Contracts;

/// <summary>The file a managing Member is about to upload, as posted to <c>POST /api/releases/{id}/tracks</c>.</summary>
public sealed class TrackUploadForm
{
    public const int FileNameMaxLength = 255;
    public const long MaxFileSize = 500L * 1024 * 1024;
    public const int MaxTracksPerRelease = 50;

    [Required, StringLength(FileNameMaxLength)]
    public string FileName { get; set; } = "";

    [Range(1, MaxFileSize, ErrorMessage = "A Track's file can be at most 500 MB.")]
    public long Size { get; set; }
}
