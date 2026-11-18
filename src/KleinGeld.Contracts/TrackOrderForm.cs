namespace KleinGeld.Contracts;

/// <summary>Every Track of a Release in its new order, as put to <c>PUT /api/releases/{id}/tracks/order</c>.</summary>
public sealed class TrackOrderForm
{
    public List<Guid> TrackIds { get; set; } = [];
}
