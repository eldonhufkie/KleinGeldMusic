using KleinGeld.Data;
using KleinGeld.Web.Api;
using KleinGeld.Web.Components.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace KleinGeld.Web;

internal static class MemberAuthenticationExtensions
{
    /// <summary>ASP.NET Core Identity with a same-origin login cookie, as in the Blazor Web App template.</summary>
    public static void AddMemberAuthentication(this WebApplicationBuilder builder)
    {
        // On the server the keys folder is a bind-mounted dataset, so login cookies and emailed links
        // survive a redeploy. Without it (local runs) ASP.NET Core keeps its default per-user key ring.
        var keysPath = builder.Configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrEmpty(keysPath))
        {
            builder.Services.AddDataProtection()
                .SetApplicationName("KleinGeld")
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

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
            .AddDefaultTokenProviders()
            // Replaces the default provider the line above registered, so emailed links follow TimeProvider.
            .AddTokenProvider<MemberTokenProvider>(TokenOptions.DefaultProvider);

        builder.Services.AddSingleton<IEmailSender<Member>, IdentityNoOpEmailSender>();

        // Every emailed link (set password, reset password, change email) works for 15 minutes.
        builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromMinutes(15));

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
