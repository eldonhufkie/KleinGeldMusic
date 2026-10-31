using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KleinGeld.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without a running database.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<KleinGeldDbContext>
{
    public KleinGeldDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<KleinGeldDbContext>()
            .UseNpgsql("Host=localhost;Database=kleingeld")
            .Options);
}
