namespace KleinGeld.Contracts;

/// <summary>
/// An approved Member who hasn't set a password yet, as listed by <c>GET /api/admin/members/awaiting-password</c>.
/// </summary>
public sealed record MemberAwaitingPassword(Guid Id, string Email);
