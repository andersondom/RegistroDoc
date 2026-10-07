using Microsoft.EntityFrameworkCore;
using RegistroDoc.Domain.Entities;
using RegistroDoc.Infrastructure.Persistence;

namespace RegistroDoc.IntegrationTests;

public sealed class DocumentoSearchIntegrationTests
{
    [Fact]
    public async Task PesquisaTextual_DeveLocalizarDocumentoEPagina()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__RegistroDoc");

        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<RegistroDocDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new RegistroDocDbContext(options);

        var serventiaId = await db.Serventias
            .Where(item => item.CodigoCns == "TESTE0001")
            .Select(item => item.Id)
            .SingleAsync();

        var termo = $"REGISTRODOC_PESQUISA_{Guid.NewGuid():N}";
        var documento = new Documento
        {
            ServentiaId = serventiaId,
            TipoDocumento = "HabilitacaoCasamento",
            NomeArquivo = $"PESQUISA_{Guid.NewGuid():N}.pdf",
            CaminhoRelativo = "testes/pesquisa.pdf",
            AnoReferencia = 2026,
            TamanhoBytes = 123,
            HashSha256 = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(termo))),
            QuantidadePaginas = 2,
            StatusIndexacao = "Concluido",
            IndexadoEmUtc = DateTime.UtcNow
        };

        db.Documentos.Add(documento);
        db.PaginasDocumento.AddRange(
            new PaginaDocumento
            {
                DocumentoId = documento.Id,
                NumeroPagina = 1,
                TextoExtraido = "Página sem o conteúdo procurado."
            },
            new PaginaDocumento
            {
                DocumentoId = documento.Id,
                NumeroPagina = 2,
                TextoExtraido =
                    $"Conteúdo fictício para validação {termo} do RegistroDoc."
            });

        await db.SaveChangesAsync();

        try
        {
            var encontrados = await db.Documentos
                .AsNoTracking()
                .Where(item =>
                    item.StatusIndexacao == "Concluido" &&
                    db.PaginasDocumento.Any(pagina =>
                        pagina.DocumentoId == item.Id &&
                        EF.Functions.ILike(
                            pagina.TextoExtraido,
                            $"%{termo.ToLowerInvariant()}%")))
                .Select(item => item.Id)
                .ToListAsync();

            var paginas = await db.PaginasDocumento
                .AsNoTracking()
                .Where(item =>
                    item.DocumentoId == documento.Id &&
                    EF.Functions.ILike(
                        item.TextoExtraido,
                        $"%{termo.ToLowerInvariant()}%"))
                .Select(item => item.NumeroPagina)
                .ToListAsync();

            Assert.Contains(documento.Id, encontrados);
            Assert.Equal([2], paginas);
        }
        finally
        {
            await db.PaginasDocumento
                .Where(item => item.DocumentoId == documento.Id)
                .ExecuteDeleteAsync();

            await db.Documentos
                .Where(item => item.Id == documento.Id)
                .ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Pesquisa_DeveRespeitarFiltrosDeTipoEAno()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__RegistroDoc");

        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<RegistroDocDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new RegistroDocDbContext(options);

        var serventiaId = await db.Serventias
            .Where(item => item.CodigoCns == "TESTE0001")
            .Select(item => item.Id)
            .SingleAsync();

        var termo = $"REGISTRODOC_FILTRO_{Guid.NewGuid():N}";
        var documento = new Documento
        {
            ServentiaId = serventiaId,
            TipoDocumento = "HabilitacaoCasamento",
            NomeArquivo = $"FILTRO_{Guid.NewGuid():N}.pdf",
            CaminhoRelativo = "testes/filtro.pdf",
            AnoReferencia = 2026,
            TamanhoBytes = 123,
            HashSha256 = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(termo))),
            QuantidadePaginas = 1,
            StatusIndexacao = "Concluido",
            IndexadoEmUtc = DateTime.UtcNow
        };

        db.Documentos.Add(documento);
        db.PaginasDocumento.Add(new PaginaDocumento
        {
            DocumentoId = documento.Id,
            NumeroPagina = 1,
            TextoExtraido = termo
        });

        await db.SaveChangesAsync();

        try
        {
            var correto = await db.Documentos
                .AsNoTracking()
                .CountAsync(item =>
                    item.Id == documento.Id &&
                    item.TipoDocumento == "HabilitacaoCasamento" &&
                    item.AnoReferencia == 2026);

            var anoIncorreto = await db.Documentos
                .AsNoTracking()
                .CountAsync(item =>
                    item.Id == documento.Id &&
                    item.AnoReferencia == 2025);

            var tipoIncorreto = await db.Documentos
                .AsNoTracking()
                .CountAsync(item =>
                    item.Id == documento.Id &&
                    item.TipoDocumento == "Nascimento");

            Assert.Equal(1, correto);
            Assert.Equal(0, anoIncorreto);
            Assert.Equal(0, tipoIncorreto);
        }
        finally
        {
            await db.PaginasDocumento
                .Where(item => item.DocumentoId == documento.Id)
                .ExecuteDeleteAsync();

            await db.Documentos
                .Where(item => item.Id == documento.Id)
                .ExecuteDeleteAsync();
        }
    }
}
