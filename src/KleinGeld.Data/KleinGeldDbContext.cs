using KleinGeld.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KleinGeld.Data;

public sealed class KleinGeldDbContext(DbContextOptions<KleinGeldDbContext> options)
    : IdentityDbContext<Member, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<JoinRequest> JoinRequests => Set<JoinRequest>();

    public DbSet<Artist> Artists => Set<Artist>();

    public DbSet<Release> Releases => Set<Release>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<JoinRequest>(joinRequest =>
        {
            joinRequest.Property(r => r.Name).HasMaxLength(JoinRequestForm.NameMaxLength);
            joinRequest.Property(r => r.Email).HasMaxLength(JoinRequestForm.EmailMaxLength);
            joinRequest.Property(r => r.NormalizedEmail).HasMaxLength(JoinRequestForm.EmailMaxLength);
            joinRequest.Property(r => r.Message).HasMaxLength(JoinRequestForm.MessageMaxLength);
            joinRequest.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            joinRequest.HasIndex(r => new { r.NormalizedEmail, r.Status });
        });

        builder.Entity<Artist>(artist =>
        {
            artist.Property(a => a.Name).HasMaxLength(ArtistForm.NameMaxLength);
            artist.Property(a => a.Slug).HasMaxLength(ArtistForm.NameMaxLength);
            artist.HasMany(a => a.Managers).WithMany().UsingEntity("ArtistManagers");
        });

        builder.Entity<Release>(release =>
        {
            release.Property(r => r.Title).HasMaxLength(ReleaseForm.TitleMaxLength);
            release.Property(r => r.Slug).HasMaxLength(ReleaseForm.TitleMaxLength);
            release.Property(r => r.Type).HasConversion<string>().HasMaxLength(16);
            release.Property(r => r.Description).HasMaxLength(ReleaseForm.DescriptionMaxLength);
            release.Property(r => r.Genre).HasMaxLength(ReleaseForm.GenreMaxLength);
            release.Property(r => r.Stage).HasConversion<string>().HasMaxLength(16);
            release.HasOne(r => r.Artist).WithMany(a => a.Releases).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
