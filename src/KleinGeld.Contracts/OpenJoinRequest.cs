namespace KleinGeld.Contracts;

/// <summary>A Join Request waiting for the Admin, as listed by <c>GET /api/admin/join-requests</c>.</summary>
public sealed record OpenJoinRequest(
    Guid Id,
    string Name,
    string Email,
    string Message,
    DateTimeOffset CreatedAt,
    bool EmailBelongsToMember,
    bool DuplicateOfOpenRequest);
