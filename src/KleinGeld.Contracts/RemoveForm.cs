using System.ComponentModel.DataAnnotations;

namespace KleinGeld.Contracts;

/// <summary>The Admin Removing a Member, as posted to <c>POST /api/admin/members/{id}/remove</c>.</summary>
public sealed class RemoveForm
{
    public const int ReasonMaxLength = 2000;

    [Required(ErrorMessage = "Say why they are Removed."), StringLength(ReasonMaxLength)]
    public string Reason { get; set; } = "";
}
