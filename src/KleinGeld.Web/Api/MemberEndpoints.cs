using System.Security.Claims;
using KleinGeld.Contracts;
using KleinGeld.Data;
using Microsoft.AspNetCore.Identity;
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

        // What Leaving would delete, for the confirmation.
        api.MapGet("/me/leave", (ClaimsPrincipal member, KleinGeldDbContext db) => db.PreviewAsync(member.MemberId()));

        // Leaving for good, after re-checking the password or a passkey. No email: they asked for it.
        api.MapPost("/me/leave", async (
            LeaveForm form, ClaimsPrincipal caller, KleinGeldDbContext db, UserManager<Member> members,
            SignInManager<Member> signIn, [AsParameters] RemovalStores stores) =>
        {
            var memberId = caller.MemberId();
            if (await db.IsAdminAsync(memberId))
            {
                return Results.Problem("The Admin can't Leave.", statusCode: StatusCodes.Status409Conflict);
            }

            var member = await members.FindByIdAsync(memberId.ToString());
            if (member is null || !await IsConfirmedAsync(form, member, members, signIn))
            {
                return Results.Problem("That password or passkey isn't right.", statusCode: StatusCodes.Status400BadRequest);
            }

            await db.DeleteAsync(stores, memberId);
            await signIn.SignOutAsync();
            return Results.NoContent();
        });
    }

    private static async Task<bool> IsConfirmedAsync(LeaveForm form, Member member, UserManager<Member> members, SignInManager<Member> signIn)
    {
        if (!string.IsNullOrEmpty(form.Password))
        {
            return await members.CheckPasswordAsync(member, form.Password);
        }

        if (!string.IsNullOrEmpty(form.PasskeyCredentialJson))
        {
            var assertion = await signIn.PerformPasskeyAssertionAsync(form.PasskeyCredentialJson);
            return assertion.Succeeded && assertion.User.Id == member.Id;
        }

        return false;
    }
}
