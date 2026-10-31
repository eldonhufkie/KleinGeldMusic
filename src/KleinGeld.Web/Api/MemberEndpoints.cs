using System.Security.Claims;
using KleinGeld.Contracts;

namespace KleinGeld.Web.Api;

internal static class MemberEndpoints
{
    /// <summary>Endpoints about the logged-in Member themselves.</summary>
    public static void MapMemberEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", (ClaimsPrincipal member) =>
            new MeResponse(member.FindFirstValue(ClaimTypes.Email) ?? "", member.IsInRole(Roles.Admin)));
    }
}
