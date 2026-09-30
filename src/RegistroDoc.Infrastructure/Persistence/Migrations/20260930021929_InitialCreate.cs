using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegistroDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Entidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntidadeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Acao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UsuarioId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Descricao = table.Column<string>(type: "text", nullable: true),
                    OcorridaEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Serventias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CodigoCns = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Municipio = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    CriadaEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Serventias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServentiaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoDocumento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CaminhoRelativo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AnoReferencia = table.Column<int>(type: "integer", nullable: true),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    HashSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    QuantidadePaginas = table.Column<int>(type: "integer", nullable: false),
                    StatusIndexacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IndexadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documentos_Serventias_ServentiaId",
                        column: x => x.ServentiaId,
                        principalTable: "Serventias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecucoesIndexacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IniciadaEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinalizadaEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaginasProcessadas = table.Column<int>(type: "integer", nullable: false),
                    MensagemErro = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecucoesIndexacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecucoesIndexacao_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaginasDocumento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroPagina = table.Column<int>(type: "integer", nullable: false),
                    TextoExtraido = table.Column<string>(type: "text", nullable: false),
                    ExtraidaEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaginasDocumento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaginasDocumento_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_OcorridaEmUtc",
                table: "Auditorias",
                column: "OcorridaEmUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_HashSha256",
                table: "Documentos",
                column: "HashSha256");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_ServentiaId",
                table: "Documentos",
                column: "ServentiaId");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_StatusIndexacao",
                table: "Documentos",
                column: "StatusIndexacao");

            migrationBuilder.CreateIndex(
                name: "IX_ExecucoesIndexacao_DocumentoId",
                table: "ExecucoesIndexacao",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_PaginasDocumento_DocumentoId_NumeroPagina",
                table: "PaginasDocumento",
                columns: new[] { "DocumentoId", "NumeroPagina" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Serventias_CodigoCns",
                table: "Serventias",
                column: "CodigoCns",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditorias");

            migrationBuilder.DropTable(
                name: "ExecucoesIndexacao");

            migrationBuilder.DropTable(
                name: "PaginasDocumento");

            migrationBuilder.DropTable(
                name: "Documentos");

            migrationBuilder.DropTable(
                name: "Serventias");
        }
    }
}
