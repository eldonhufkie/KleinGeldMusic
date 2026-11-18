namespace KleinGeld.Contracts;

/// <summary>A Member as the Admin sees them in <c>GET /api/admin/members</c>, to pick one to Remove.</summary>
public sealed record MemberListing(Guid Id, string Email, bool IsAdmin);
