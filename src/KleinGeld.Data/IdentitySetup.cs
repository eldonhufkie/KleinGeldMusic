using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace KleinGeld.Data;

public static class IdentitySetup
{
    /// <summary>Identity for <see cref="Member"/>s with roles, stored in <see cref="KleinGeldDbContext"/>.</summary>
    public static IdentityBuilder AddMemberIdentityCore(this IServiceCollection services, Action<IdentityOptions>? configure = null) =>
        services.AddIdentityCore<Member>(options =>
            {
                ConfigureStores(options);
                configure?.Invoke(options);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<KleinGeldDbContext>();

    /// <summary>
    /// Identity options that shape the database model. Every host that builds a <see cref="KleinGeldDbContext"/>
    /// (web, migrator, design time) must use them, or EF Core sees a different model than the migrations.
    /// </summary>
    public static void ConfigureStores(IdentityOptions options)
    {
        // Version 3 adds the passkeys table.
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    }
}
