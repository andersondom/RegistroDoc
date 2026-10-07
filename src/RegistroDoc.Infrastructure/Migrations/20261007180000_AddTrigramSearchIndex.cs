using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RegistroDoc.Infrastructure.Persistence;

#nullable disable

namespace RegistroDoc.Infrastructure.Migrations;

[DbContext(typeof(RegistroDocDbContext))]
[Migration("20261007180000_AddTrigramSearchIndex")]
public partial class AddTrigramSearchIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_PaginasDocumento_TextoExtraido_Trgm"
            ON "PaginasDocumento" USING GIN ("TextoExtraido" gin_trgm_ops);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_PaginasDocumento_TextoExtraido_Trgm";
            """);
        // Keep pg_trgm: other database objects may depend on the extension.
    }
}
