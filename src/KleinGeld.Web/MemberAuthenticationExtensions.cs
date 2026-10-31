using KleinGeld.Data;
using KleinGeld.Web.Api;
using KleinGeld.Web.Components.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace KleinGeld.Web;

internal static class MemberAuthenticationExtensions
{
    /// <summary>ASP.NET Core Identity with a same-origin login cookie, as in the Blazor Web App template.</summary>
    public static void AddMemberAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<IdentityRedirectManager>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();
        builder.Services.AddAuthorization();

        builder.Services.AddMemberIdentityCore(options => options.SignIn.RequireConfirmedAccount = true)
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddSingleton<IEmailSender<Member>, IdentityNoOpEmailSender>();

        // Check the security stamp on every request, so a Removed Member or a changed password
        // cuts off existing sessions at once instead of after Identity's default 30 minutes.
        builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

        // API calls answer 401/403; only pages redirect to the Login page.
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Events.OnRedirectToLogin = context => RespondOrRedirect(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => RespondOrRedirect(context, StatusCodes.Status403Forbidden);
        });
    }

    private static Task RespondOrRedirect(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        if (context.Request.IsApiRequest())
        {
            context.Response.StatusCode = statusCode;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }
        return Task.CompletedTask;
    }
}
