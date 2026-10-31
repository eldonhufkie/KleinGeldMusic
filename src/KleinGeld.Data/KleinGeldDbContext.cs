using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Data;

public sealed class KleinGeldDbContext(DbContextOptions<KleinGeldDbContext> options) : DbContext(options);
