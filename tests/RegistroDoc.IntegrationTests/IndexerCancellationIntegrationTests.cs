using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RegistroDoc.Indexer;
using RegistroDoc.Indexer.Options;
using RegistroDoc.Indexer.Services;
using RegistroDoc.Infrastructure.Persistence;
using RegistroDoc.IntegrationTests.TestDoubles;

namespace RegistroDoc.IntegrationTests;

public sealed class IndexerCancellationIntegrationTests
{
    [Fact]
    public async Task Cancelamento_DeveLiberarDocumentoParaNovaTentativa()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__RegistroDoc");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var pasta =
            Path.Combine(
                Path.GetTempPath(),
                "RegistroDoc.Tests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(pasta);

        var pdf =
            Path.Combine(
                pasta,
                "CANCELAMENTO_FICTICIO.pdf");

        await File.WriteAllTextAsync(
            pdf,
            "PDF FICTICIO PARA TESTE DE CANCELAMENTO");

        var inspector =
            new PdfFileInspector();

        var info =
            await inspector.InspectAsync(
                pdf,
                pasta);

        var options =
            new IndexerOptions
            {
                InputPath = pasta,
                SearchPattern = "*.pdf",
                IncludeSubdirectories = false,
                ScanIntervalSeconds = 60,
                CodigoCnsServentia = "TESTE0001",
                TipoDocumento = "HabilitacaoCasamento",
                AnoReferencia = 2026,
                ProcessingTimeoutMinutes = 30
            };

        var extractor =
            new BlockingPdfTextExtractor();

        var services =
            new ServiceCollection();

        services.AddDbContext<RegistroDocDbContext>(
            db =>
                db.UseNpgsql(connectionString));

        await using var provider =
            services.BuildServiceProvider();

        var scopeFactory =
            provider.GetRequiredService<
                IServiceScopeFactory>();

        var environment =
            new TestHostEnvironment
            {
                ContentRootPath =
                    Directory.GetCurrentDirectory()
            };

        var worker =
            new Worker(
                NullLogger<Worker>.Instance,
                Options.Create(options),
                environment,
                inspector,
                extractor,
                scopeFactory);

        try
        {
            await LimparAsync(
                provider,
                info.HashSha256);

            using var timeout =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(30));

            await worker.StartAsync(
                timeout.Token);

            await extractor.Started.WaitAsync(
                timeout.Token);

            await AguardarEstadoAsync(
                provider,
                info.HashSha256,
                "Processando",
                timeout.Token);

            await worker.StopAsync(
                CancellationToken.None);

            await AguardarEstadoAsync(
                provider,
                info.HashSha256,
                "Pendente",
                timeout.Token);

            await using var scope =
                provider.CreateAsyncScope();

            var db =
                scope.ServiceProvider
                    .GetRequiredService<
                        RegistroDocDbContext>();

            var documento =
                await db.Documentos
                    .AsNoTracking()
                    .SingleAsync(
                        item =>
                            item.HashSha256 ==
                            info.HashSha256,
                        timeout.Token);

            var execucao =
                await db.ExecucoesIndexacao
                    .AsNoTracking()
                    .SingleAsync(
                        item =>
                            item.DocumentoId ==
                            documento.Id,
                        timeout.Token);

            var paginas =
                await db.PaginasDocumento
                    .AsNoTracking()
                    .CountAsync(
                        item =>
                            item.DocumentoId ==
                            documento.Id,
                        timeout.Token);

            Assert.Equal(
                "Pendente",
                documento.StatusIndexacao);

            Assert.Equal(
                "Pendente",
                execucao.Status);

            Assert.Null(
                execucao.FinalizadaEmUtc);

            Assert.Null(
                execucao.MensagemErro);

            Assert.Equal(
                0,
                paginas);
        }
        finally
        {
            try
            {
                await worker.StopAsync(
                    CancellationToken.None);
            }
            catch
            {
                // Worker pode já estar parado.
            }

            await LimparAsync(
                provider,
                info.HashSha256);

            if (Directory.Exists(pasta))
            {
                Directory.Delete(
                    pasta,
                    recursive: true);
            }
        }
    }

    private static async Task AguardarEstadoAsync(
        ServiceProvider provider,
        string hash,
        string estadoEsperado,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            await using var scope =
                provider.CreateAsyncScope();

            var db =
                scope.ServiceProvider
                    .GetRequiredService<
                        RegistroDocDbContext>();

            var estado =
                await db.Documentos
                    .AsNoTracking()
                    .Where(item =>
                        item.HashSha256 == hash)
                    .Select(item =>
                        item.StatusIndexacao)
                    .SingleOrDefaultAsync(
                        cancellationToken);

            if (estado == estadoEsperado)
            {
                return;
            }

            await Task.Delay(
                50,
                cancellationToken);
        }
    }

    private static async Task LimparAsync(
        ServiceProvider provider,
        string hash)
    {
        await using var scope =
            provider.CreateAsyncScope();

        var db =
            scope.ServiceProvider
                .GetRequiredService<
                    RegistroDocDbContext>();

        var documento =
            await db.Documentos
                .SingleOrDefaultAsync(
                    item =>
                        item.HashSha256 == hash);

        if (documento is null)
        {
            return;
        }

        var execucoes =
            await db.ExecucoesIndexacao
                .Where(item =>
                    item.DocumentoId ==
                    documento.Id)
                .ToListAsync();

        var paginas =
            await db.PaginasDocumento
                .Where(item =>
                    item.DocumentoId ==
                    documento.Id)
                .ToListAsync();

        db.ExecucoesIndexacao
            .RemoveRange(execucoes);

        db.PaginasDocumento
            .RemoveRange(paginas);

        db.Documentos.Remove(
            documento);

        await db.SaveChangesAsync();
    }

    private sealed class TestHostEnvironment
        : IHostEnvironment
    {
        public string EnvironmentName
        {
            get;
            set;
        } = Environments.Development;

        public string ApplicationName
        {
            get;
            set;
        } = "RegistroDoc.IntegrationTests";

        public string ContentRootPath
        {
            get;
            set;
        } = string.Empty;

        public IFileProvider ContentRootFileProvider
        {
            get;
            set;
        } =
            new Microsoft.Extensions.FileProviders
                .NullFileProvider();
    }
}