using System.ComponentModel.DataAnnotations;

namespace KleinGeld.Contracts;

/// <summary>
/// What an outsider fills in on the landing page, as posted to <c>POST /api/join-requests</c>. The same rules
/// validate the form in the browser and the request on the server.
/// </summary>
public sealed class JoinRequestForm
{
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 256;
    public const int MessageMaxLength = 2000;

    [Required, StringLength(NameMaxLength)]
    public string Name { get; set; } = "";

    [Required, EmailAddress, StringLength(EmailMaxLength)]
    public string Email { get; set; } = "";

    [Required, StringLength(MessageMaxLength)]
    public string Message { get; set; } = "";
}
