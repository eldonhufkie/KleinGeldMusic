using System.Security.Claims;
using KleinGeld.Contracts;
using KleinGeld.Data;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Web.Api;

internal static class MemberEndpoints
{
    /// <summary>Endpoints about the logged-in Member themselves.</summary>
    public static void MapMemberEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", async (ClaimsPrincipal member, KleinGeldDbContext db) =>
            new MeResponse(
                member.FindFirstValue(ClaimTypes.Email) ?? "",
                member.IsInRole(Roles.Admin),
                await db.Artists.ManagedBy(member.MemberId()).AnyAsync()));
    }
}
