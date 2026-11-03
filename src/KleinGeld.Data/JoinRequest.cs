namespace KleinGeld.Data;

/// <summary>A request from someone outside the group to become a Member, which the Admin approves or rejects.</summary>
public sealed class JoinRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Name { get; init; }

    public required string Email { get; init; }

    /// <summary>
    /// <see cref="Email"/> normalized the way Identity normalizes a Member's email, for comparing with Members
    /// and other Join Requests.
    /// </summary>
    public required string NormalizedEmail { get; init; }

    public required string Message { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Open;

    /// <summary>The email already belonged to a Member when the Join Request arrived.</summary>
    public bool EmailBelongsToMember { get; init; }

    /// <summary>Another open Join Request had the same email when this one arrived.</summary>
    public bool DuplicateOfOpenRequest { get; init; }
}

public enum JoinRequestStatus
{
    Open,
    Approved,
    Rejected,
}
