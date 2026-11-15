namespace KleinGeld.Contracts;

/// <summary>
/// A Member re-checking who they are to Leave, as posted to <c>POST /api/me/leave</c>: their password, or a passkey
/// assertion (the credential JSON from <c>navigator.credentials.get</c>) answering <c>/Account/PasskeyRequestOptions</c>.
/// </summary>
public sealed class LeaveForm
{
    public string? Password { get; set; }

    public string? PasskeyCredentialJson { get; set; }
}
