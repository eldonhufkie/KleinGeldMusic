using System.ComponentModel.DataAnnotations;

namespace KleinGeld.Contracts;

/// <summary>The Admin's Reject, as posted to <c>POST /api/admin/review/releases/{id}/reject</c>.</summary>
public sealed class RejectForm
{
    public const int ReasonMaxLength = 2000;

    [Required(ErrorMessage = "Say why the Release is rejected."), StringLength(ReasonMaxLength)]
    public string Reason { get; set; } = "";
}
