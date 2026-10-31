namespace KleinGeld.Contracts;

/// <summary>The logged-in Member, as returned by <c>GET /api/me</c>.</summary>
public sealed record MeResponse(string Email, bool IsAdmin);
