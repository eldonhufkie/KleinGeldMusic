using KleinGeld.Contracts;
using KleinGeld.Data;
using Microsoft.AspNetCore.Identity;

namespace KleinGeld.Migrator;

/// <summary>
/// Makes sure exactly one Admin exists. The first run creates it from configuration; once an Admin exists,
/// changing the configured email does nothing (handing the role over is a manual database change).
/// </summary>
internal sealed class AdminSeeder(
    UserManager<Member> members,
    RoleManager<IdentityRole<Guid>> roles,
    IConfiguration configuration,
    ILogger<AdminSeeder> logger)
{
    public async Task EnsureAdminAsync()
    {
        var email = configuration["Admin:Email"];
        var password = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Admin:Email and Admin:Password must be configured.");
        }

        if (!await roles.RoleExistsAsync(Roles.Admin))
        {
            ThrowIfFailed(await roles.CreateAsync(new IdentityRole<Guid>(Roles.Admin) { Id = Guid.CreateVersion7() }));
        }

        var admins = await members.GetUsersInRoleAsync(Roles.Admin);
        if (admins.Count > 0)
        {
            if (!admins.Any(admin => string.Equals(admin.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogWarning("An Admin already exists, so the configured Admin email {Email} is ignored.", email);
            }
            return;
        }

        var member = await members.FindByEmailAsync(email);
        if (member is null)
        {
            member = new Member { UserName = email, Email = email, EmailConfirmed = true };
            ThrowIfFailed(await members.CreateAsync(member, password));
        }

        ThrowIfFailed(await members.AddToRoleAsync(member, Roles.Admin));
        logger.LogInformation("Created the Admin {Email}.", email);
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }
}
