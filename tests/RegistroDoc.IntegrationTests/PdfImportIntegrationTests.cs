using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using RegistroDoc.Api.Controllers;
using RegistroDoc.Contracts.Search;
using RegistroDoc.Indexer.Importing;
using RegistroDoc.Indexer.Services;
using RegistroDoc.Infrastructure.Persistence;

namespace RegistroDoc.IntegrationTests;

public sealed class PdfImportIntegrationTests
{
    [Fact]
    public async Task SimulacaoECarga_DevemPreservarOrigemIndexarPesquisarERetomarSemDuplicar()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RegistroDoc");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var temporary = Path.Combine(Path.GetTempPath(), "registrodoc-import-test-" + Guid.NewGuid());
        var source = Path.Combine(temporary, "source");
        var target = Path.Combine(temporary, "target");
        var type = "Categoria_" + Guid.NewGuid().ToString("N");
        var phrase = "MIGRACAO_FICTICIA_" + Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<RegistroDocDbContext>(o => o.UseNpgsql(connection));
        await using var provider = services.BuildServiceProvider();
        var importer = new PdfBatchImporter(new PdfFileInspector(), new PdfPigTextExtractor(),
            provider.GetRequiredService<IServiceScopeFactory>(), new TestEnvironment());
        var options = new ImportOptions { SourcePath = source, TargetRootPath = target,
            ReportDirectory = Path.Combine(temporary, "reports"), CodigoCnsServentia = "TESTE0001",
            Categorias = new() { ["Categoria"] = new() { TipoDocumento = type, AnoPorSubpasta = true } } };
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "Categoria", "2025"));
            var original = Path.Combine(source, "Categoria", "2025", "PROCESSO-FICTICIO.PDF");
            var bytes = CreatePdf(phrase);
            await File.WriteAllBytesAsync(original, bytes);
            var plan = await importer.RunAsync(options, execute: false);
            Assert.Equal(new ImportSummary(1, 1, 0, 0, 0), plan);
            options.ApprovedPlanPath = Directory.GetFiles(options.ReportDirectory, "*.ndjson").Single();
            Assert.False(Directory.Exists(target));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => importer.RunAsync(options, execute: true,
                authorize: _ => throw new UnauthorizedAccessException()));
            Assert.False(Directory.Exists(target));
            Assert.Single(Directory.GetFiles(options.ReportDirectory, "*.ndjson"));
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RegistroDocDbContext>();
            Assert.False(await db.Documentos.AnyAsync(d => d.TipoDocumento == type));

            Assert.Equal(new ImportSummary(1, 0, 1, 0, 0), await importer.RunAsync(options, execute: true));
            Assert.Equal(new ImportSummary(1, 0, 0, 1, 0), await importer.RunAsync(options, execute: true));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(original));
            var document = await db.Documentos.AsNoTracking().SingleAsync(d => d.TipoDocumento == type);
            Assert.Equal("Concluido", document.StatusIndexacao);
            Assert.Equal(2025, document.AnoReferencia);
            Assert.Equal("PROCESSO-FICTICIO.PDF", document.NomeArquivo);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(target, document.CaminhoRelativo)));
            Assert.Contains(phrase, (await db.PaginasDocumento.SingleAsync(p => p.DocumentoId == document.Id)).TextoExtraido);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Documents:RootPath"] = target }).Build();
            var controller = new DocumentosController(db, configuration);
            var search = await controller.Pesquisar(new DocumentoSearchRequest { Termo = phrase, TipoDocumento = type, AnoReferencia = 2025 }, default);
            var response = Assert.IsType<OkObjectResult>(search.Result);
            Assert.Single(Assert.IsType<DocumentoSearchResponse>(response.Value).Itens);
            var pdf = Assert.IsType<PhysicalFileResult>(await controller.ObterArquivo(document.Id, default));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(pdf.FileName));
            var types = Assert.IsType<OkObjectResult>((await controller.ListarTipos(default)).Result);
            Assert.Contains(type, Assert.IsAssignableFrom<IReadOnlyList<string>>(types.Value));

            options.Categorias["Categoria"].AnoPorSubpasta = false;
            options.Categorias["Categoria"].AnoReferencia = 2026;
            await importer.RunAsync(options, execute: false);
            options.ApprovedPlanPath = Directory.GetFiles(options.ReportDirectory, "*.ndjson")
                .OrderByDescending(File.GetLastWriteTimeUtc).First();
            var conflict = await importer.RunAsync(options, execute: true);
            Assert.Equal(1, conflict.Falhas);
            Assert.Equal(2025, (await db.Documentos.AsNoTracking().SingleAsync(d => d.Id == document.Id)).AnoReferencia);
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RegistroDocDbContext>();
            var ids = db.Documentos.Where(d => d.TipoDocumento == type).Select(d => d.Id);
            await db.ExecucoesIndexacao.Where(e => ids.Contains(e.DocumentoId)).ExecuteDeleteAsync();
            await db.PaginasDocumento.Where(p => ids.Contains(p.DocumentoId)).ExecuteDeleteAsync();
            await db.Documentos.Where(d => d.TipoDocumento == type).ExecuteDeleteAsync();
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    [Fact]
    public async Task Simulacao_DeveDetectarCategoriaAusentePdfSemTextoECopiaDuplicada()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "registrodoc-plan-test-" + Guid.NewGuid());
        var source = Path.Combine(temporary, "source");
        using var provider = new ServiceCollection().BuildServiceProvider();
        var importer = new PdfBatchImporter(new PdfFileInspector(), new PdfPigTextExtractor(),
            provider.GetRequiredService<IServiceScopeFactory>(), new TestEnvironment());
        var options = new ImportOptions { SourcePath = source, TargetRootPath = Path.Combine(temporary, "target"),
            ReportDirectory = Path.Combine(temporary, "reports"), CodigoCnsServentia = "TESTE0001",
            Categorias = new() { ["Categoria"] = new() { TipoDocumento = "CategoriaFicticia" } } };
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "Categoria"));
            Directory.CreateDirectory(Path.Combine(source, "Outra"));
            var bytes = CreatePdf("CONTEUDO FICTICIO DE IMPORTACAO");
            await File.WriteAllBytesAsync(Path.Combine(source, "Categoria", "A.pdf"), bytes);
            await File.WriteAllBytesAsync(Path.Combine(source, "Categoria", "B.pdf"), bytes);
            await File.WriteAllBytesAsync(Path.Combine(source, "Categoria", "vazio.pdf"), CreatePdf(""));
            await File.WriteAllBytesAsync(Path.Combine(source, "Outra", "C.pdf"), bytes);
            var result = await importer.RunAsync(options, execute: false);
            Assert.Equal(new ImportSummary(4, 1, 0, 1, 2), result);
            Assert.False(Directory.Exists(options.TargetRootPath));
            options.TargetRootPath = Path.Combine(source, "destino");
            await Assert.ThrowsAsync<ArgumentException>(() => importer.RunAsync(options, execute: false));
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    private static byte[] CreatePdf(string text)
    {
        var content = $"BT /F1 12 Tf 50 700 Td ({text}) Tj ET";
        string[] objects = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", $"<< /Length {content.Length} >>\nstream\n{content}\nendstream"];
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++) { offsets.Add(pdf.Length); pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = pdf.Length;
        pdf.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    [Fact]
    public async Task Simulacao_DeveRejeitarRaizComAncestralSimbolicoSemCriarRelatorio()
    {
        if (OperatingSystem.IsWindows()) return;
        var temporary = Path.Combine(Path.GetTempPath(), "registrodoc-import-root-" + Guid.NewGuid());
        using var provider = new ServiceCollection().BuildServiceProvider();
        var importer = new PdfBatchImporter(new PdfFileInspector(), new PdfPigTextExtractor(),
            provider.GetRequiredService<IServiceScopeFactory>(), new TestEnvironment());
        try
        {
            Directory.CreateDirectory(Path.Combine(temporary, "actual", "source"));
            Directory.CreateSymbolicLink(Path.Combine(temporary, "alias"), Path.Combine(temporary, "actual"));
            var options = new ImportOptions { SourcePath = Path.Combine(temporary, "alias", "source"),
                TargetRootPath = Path.Combine(temporary, "target"), ReportDirectory = Path.Combine(temporary, "reports"),
                CodigoCnsServentia = "TESTE0001", Categorias = new() { ["Categoria"] = new() { TipoDocumento = "Ficticio" } } };
            await Assert.ThrowsAsync<ArgumentException>(() => importer.RunAsync(options, execute: false));
            Assert.False(Directory.Exists(options.ReportDirectory));
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    [Fact]
    public async Task Simulacao_DeveExigirAnoEDetectarConflitoDeMetadadosNoLote()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "registrodoc-years-" + Guid.NewGuid());
        var source = Path.Combine(temporary, "source");
        using var provider = new ServiceCollection().BuildServiceProvider();
        var importer = new PdfBatchImporter(new PdfFileInspector(), new PdfPigTextExtractor(),
            provider.GetRequiredService<IServiceScopeFactory>(), new TestEnvironment());
        var options = new ImportOptions { SourcePath = source, TargetRootPath = Path.Combine(temporary, "target"),
            ReportDirectory = Path.Combine(temporary, "reports"), CodigoCnsServentia = "TESTE0001",
            Categorias = new() { ["Habilitações"] = new() { TipoDocumento = "HabilitacaoCasamento", AnoPorSubpasta = true } } };
        try
        {
            var bytes = CreatePdf("TEXTO FICTICIO EXCLUSIVO DO PDF");
            foreach (var year in new[] { "2025", "2026" })
            {
                Directory.CreateDirectory(Path.Combine(source, "Habilitações", year));
                await File.WriteAllBytesAsync(Path.Combine(source, "Habilitações", year, "A.pdf"), bytes);
            }
            await File.WriteAllBytesAsync(Path.Combine(source, "Habilitações", "sem-ano.pdf"), bytes);
            Assert.Equal(new ImportSummary(3, 1, 0, 0, 2), await importer.RunAsync(options, execute: false));
            var report = await File.ReadAllTextAsync(Directory.GetFiles(options.ReportDirectory).Single());
            Assert.Contains("AnoNaoIdentificado", report);
            Assert.Contains("ConflitoMetadados", report);
            Assert.Contains("\"AnoReferencia\":2025", report);
            Assert.DoesNotContain("TEXTO FICTICIO EXCLUSIVO DO PDF", report);
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    [Fact]
    public async Task Carga_DeveRejeitarDestinoComLinkAntesDeCriarPastasExternas()
    {
        if (OperatingSystem.IsWindows()) return; // Executado com links reais no Docker Linux.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RegistroDoc");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var temporary = Path.Combine(Path.GetTempPath(), "registrodoc-import-link-" + Guid.NewGuid());
        var source = Path.Combine(temporary, "source");
        var target = Path.Combine(temporary, "target");
        var outside = Path.Combine(temporary, "outside");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<RegistroDocDbContext>(o => o.UseNpgsql(connection));
        await using var provider = services.BuildServiceProvider();
        var importer = new PdfBatchImporter(new PdfFileInspector(), new PdfPigTextExtractor(),
            provider.GetRequiredService<IServiceScopeFactory>(), new TestEnvironment());
        var options = new ImportOptions { SourcePath = source, TargetRootPath = target,
            ReportDirectory = Path.Combine(temporary, "reports"), CodigoCnsServentia = "TESTE0001",
            Categorias = new() { ["Categoria"] = new() { TipoDocumento = "CategoriaFicticia" } } };
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "Categoria"));
            Directory.CreateDirectory(target);
            Directory.CreateDirectory(outside);
            Directory.CreateSymbolicLink(Path.Combine(target, "importacao"), outside);
            await File.WriteAllBytesAsync(Path.Combine(source, "Categoria", "A.pdf"), CreatePdf(Guid.NewGuid().ToString("N")));
            await importer.RunAsync(options, execute: false);
            options.ApprovedPlanPath = Directory.GetFiles(options.ReportDirectory, "*.ndjson").Single();
            Assert.Equal(1, (await importer.RunAsync(options, execute: true)).Falhas);
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    [Theory]
    [InlineData("ausente")]
    [InlineData("alterado")]
    [InlineData("adicionado")]
    [InlineData("removido")]
    [InlineData("categoria")]
    [InlineData("serventia")]
    [InlineData("banco")]
    [InlineData("ambiente")]
    [InlineData("destino")]
    [InlineData("incompleto")]
    [InlineData("com-falhas")]
    public async Task Carga_DeveExigirPlanoIntegroAntesDeAcessarBancoOuCriarDestino(string change)
    {
        var temporary = Path.Combine(Path.GetTempPath(), "registrodoc-reviewed-" + Guid.NewGuid());
        var source = Path.Combine(temporary, "source");
        using var provider = new ServiceCollection().BuildServiceProvider(); // Sem DbContext: guarda deve agir antes do banco.
        var importer = new PdfBatchImporter(new PdfFileInspector(), new PdfPigTextExtractor(),
            provider.GetRequiredService<IServiceScopeFactory>(), new TestEnvironment());
        var options = new ImportOptions { SourcePath = source, TargetRootPath = Path.Combine(temporary, "target"),
            ReportDirectory = Path.Combine(temporary, "reports"), CodigoCnsServentia = "TESTE0001",
            Categorias = new() { ["Categoria"] = new() { TipoDocumento = "CategoriaFicticia" } } };
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "Categoria"));
            var file = Path.Combine(source, "Categoria", "A.pdf");
            await File.WriteAllBytesAsync(file, CreatePdf("PLANO FICTICIO ORIGINAL"));
            if (change == "com-falhas")
                await File.WriteAllBytesAsync(Path.Combine(source, "Categoria", "sem-texto.pdf"), CreatePdf(""));
            await importer.RunAsync(options, execute: false);
            options.ApprovedPlanPath = Directory.GetFiles(options.ReportDirectory).Single();
            switch (change)
            {
                case "ausente": options.ApprovedPlanPath = ""; break;
                case "alterado": await File.WriteAllBytesAsync(file, CreatePdf("TEXTO FICTICIO MODIFICADO")); break;
                case "adicionado": await File.WriteAllBytesAsync(Path.Combine(source, "Categoria", "B.pdf"), CreatePdf("NOVO FICTICIO")); break;
                case "removido": File.Delete(file); break;
                case "categoria": options.Categorias["Categoria"].TipoDocumento = "OutraCategoria"; break;
                case "serventia": options.CodigoCnsServentia = "OUTRO_FICTICIO"; break;
                case "banco": options.DatabaseIdentity = "OUTRO_BANCO_FICTICIO"; break;
                case "ambiente": options.ExecutionContext = "OUTRO_AMBIENTE_FICTICIO"; break;
                case "destino": options.TargetRootPath = Path.Combine(temporary, "outro-target"); break;
                case "incompleto":
                    var lines = await File.ReadAllLinesAsync(options.ApprovedPlanPath);
                    await File.WriteAllLinesAsync(options.ApprovedPlanPath, lines[..^1]);
                    break;
            }
            await Assert.ThrowsAsync<ImportPlanException>(() => importer.RunAsync(options, execute: true));
            Assert.False(Directory.Exists(options.TargetRootPath));
            Assert.Single(Directory.GetFiles(options.ReportDirectory));
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "RegistroDoc.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
