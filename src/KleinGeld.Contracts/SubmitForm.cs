namespace KleinGeld.Contracts;

/// <summary>A managing Member's Submit, as posted to <c>POST /api/releases/{id}/submit</c>.</summary>
public sealed class SubmitForm
{
    /// <summary>Whether the Member ticked the Rights Declaration, shown as <see cref="RightsDeclarationWording"/>.</summary>
    public bool RightsDeclared { get; set; }
}
