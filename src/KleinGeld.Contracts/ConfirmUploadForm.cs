using System.ComponentModel.DataAnnotations;

namespace KleinGeld.Contracts;

/// <summary>Tells the API that the browser finished the upload it was given an <see cref="UploadLink"/> for.</summary>
public sealed class ConfirmUploadForm
{
    [Required]
    public Guid? UploadId { get; set; }
}
