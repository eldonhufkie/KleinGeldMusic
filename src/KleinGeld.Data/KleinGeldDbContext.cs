using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Data;

public sealed class KleinGeldDbContext(DbContextOptions<KleinGeldDbContext> options)
    : IdentityDbContext<Member, IdentityRole<Guid>, Guid>(options);
