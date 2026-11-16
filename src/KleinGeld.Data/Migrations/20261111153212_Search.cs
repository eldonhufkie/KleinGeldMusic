using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KleinGeld.Data.Migrations
{
    /// <summary>
    /// Search: <c>search_text</c> folds case and accents, and GIN trigram indexes on it serve prefix, substring and
    /// similarity matches. <c>unaccent</c> itself isn't immutable, so the wrapper names its dictionary to be indexable.
    /// </summary>
    public partial class Search : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
            migrationBuilder.Sql("""
                CREATE FUNCTION search_text(text) RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
                RETURN lower(public.unaccent('public.unaccent'::regdictionary, $1));
                """);
            migrationBuilder.Sql("""CREATE INDEX "IX_Artists_Name_Search" ON "Artists" USING gin (search_text("Name") gin_trgm_ops);""");
            migrationBuilder.Sql("""CREATE INDEX "IX_Releases_Title_Search" ON "Releases" USING gin (search_text("Title") gin_trgm_ops);""");
            migrationBuilder.Sql("""CREATE INDEX "IX_Tracks_Title_Search" ON "Tracks" USING gin (search_text("Title") gin_trgm_ops);""");
            migrationBuilder.Sql("""CREATE INDEX "IX_Tracks_FeaturedArtists_Search" ON "Tracks" USING gin (search_text("FeaturedArtists") gin_trgm_ops);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX "IX_Tracks_FeaturedArtists_Search";""");
            migrationBuilder.Sql("""DROP INDEX "IX_Tracks_Title_Search";""");
            migrationBuilder.Sql("""DROP INDEX "IX_Releases_Title_Search";""");
            migrationBuilder.Sql("""DROP INDEX "IX_Artists_Name_Search";""");
            migrationBuilder.Sql("DROP FUNCTION search_text(text);");
        }
    }
}
