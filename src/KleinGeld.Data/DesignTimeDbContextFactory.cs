using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace KleinGeld.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without a running database.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<KleinGeldDbContext>
{
    public KleinGeldDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection()
            .Configure<IdentityOptions>(IdentitySetup.ConfigureStores)
            .BuildServiceProvider();

        return new(new DbContextOptionsBuilder<KleinGeldDbContext>()
            .UseNpgsql("Host=localhost;Database=kleingeld")
            .UseApplicationServiceProvider(services)
            .Options);
    }
}
