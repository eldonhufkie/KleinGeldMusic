using System.ComponentModel.DataAnnotations;

namespace KleinGeld.Contracts;

/// <summary>The Admin deleting a Release or Artist, as posted to <c>POST /api/admin/releases/{id}/delete</c> or <c>POST /api/admin/artists/{id}/delete</c>.</summary>
public sealed class DeleteForm
{
    public const int ReasonMaxLength = 2000;

    [Required(ErrorMessage = "Say why it is deleted."), StringLength(ReasonMaxLength)]
    public string Reason { get; set; } = "";
}
