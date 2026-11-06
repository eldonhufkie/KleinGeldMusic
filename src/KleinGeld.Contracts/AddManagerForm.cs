namespace KleinGeld.Contracts;

/// <summary>The Member picked to manage an Artist, as posted to <c>POST /api/artists/{id}/managers</c>.</summary>
public sealed class AddManagerForm
{
    public Guid MemberId { get; set; }
}
