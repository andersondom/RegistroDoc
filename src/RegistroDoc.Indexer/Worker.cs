using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RegistroDoc.Domain.Entities;
using RegistroDoc.Indexer.Options;
using RegistroDoc.Indexer.Services;
using RegistroDoc.Infrastructure.Persistence;

namespace RegistroDoc.Indexer;

public sealed class Worker : BackgroundService
{
    private const string CodigoCnsServentiaTeste = "TESTE0001";

    private readonly ILogger<Worker> _logger;
    private readonly IndexerOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly IPdfFileInspector _pdfFileInspector;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(
        ILogger<Worker> logger,
        IOptions<IndexerOptions> options,
        IHostEnvironment environment,
        IPdfFileInspector pdfFileInspector,
        IPdfTextExtractor pdfTextExtractor,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _environment = environment;
        _pdfFileInspector = pdfFileInspector;
        _pdfTextExtractor = pdfTextExtractor;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var inputPath = Path.GetFullPath(
            _options.InputPath,
            _environment.ContentRootPath);

        Directory.CreateDirectory(inputPath);

        _logger.LogInformation(
            "RegistroDoc Indexer iniciado.");

        _logger.LogInformation(
            "Pasta monitorada: {InputPath}",
            inputPath);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessarPastaAsync(
                inputPath,
                stoppingToken);

            await Task.Delay(
                TimeSpan.FromSeconds(
                    _options.ScanIntervalSeconds),
                stoppingToken);
        }
    }

    private async Task ProcessarPastaAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        var searchOption =
            _options.IncludeSubdirectories
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

        string[] arquivos;

        try
        {
            arquivos = Directory.GetFiles(
                inputPath,
                _options.SearchPattern,
                searchOption);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Falha ao consultar a pasta de entrada.");

            return;
        }

        _logger.LogInformation(
            "Varredura concluída. PDFs encontrados: {Quantidade}.",
            arquivos.Length);

        foreach (var arquivo in arquivos)
        {
            try
            {
                await ProcessarArquivoAsync(
                    arquivo,
                    inputPath,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Falha ao processar PDF {Arquivo}.",
                    arquivo);
            }
        }
    }

    private async Task ProcessarArquivoAsync(
        string arquivo,
        string inputPath,
        CancellationToken cancellationToken)
    {
        var pdf =
            await _pdfFileInspector.InspectAsync(
                arquivo,
                inputPath,
                cancellationToken);

        await using var scope =
            _scopeFactory.CreateAsyncScope();

        var db =
            scope.ServiceProvider
                .GetRequiredService<RegistroDocDbContext>();

        var documento =
            await db.Documentos
                .FirstOrDefaultAsync(
                    item =>
                        item.HashSha256 ==
                        pdf.HashSha256,
                    cancellationToken);

        if (documento is null)
        {
            documento =
                await CriarDocumentoAsync(
                    db,
                    pdf,
                    cancellationToken);
        }
        else if (
            string.Equals(
                documento.StatusIndexacao,
                "Concluido",
                StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "PDF já indexado. " +
                "DocumentoId: {DocumentoId} | " +
                "Arquivo: {NomeArquivo} | " +
                "Páginas: {QuantidadePaginas}",
                documento.Id,
                documento.NomeArquivo,
                documento.QuantidadePaginas);

            return;
        }

        await IndexarDocumentoAsync(
            db,
            documento,
            arquivo,
            cancellationToken);
    }

    private async Task<Documento> CriarDocumentoAsync(
        RegistroDocDbContext db,
        PdfFileInfo pdf,
        CancellationToken cancellationToken)
    {
        var serventia =
            await db.Serventias
                .SingleOrDefaultAsync(
                    item =>
                        item.CodigoCns ==
                        CodigoCnsServentiaTeste,
                    cancellationToken);

        if (serventia is null)
        {
            throw new InvalidOperationException(
                $"Serventia fictícia {CodigoCnsServentiaTeste} não encontrada.");
        }

        var documento = new Documento
        {
            Id = Guid.NewGuid(),
            ServentiaId = serventia.Id,
            TipoDocumento = "HabilitacaoCasamento",
            NomeArquivo = pdf.NomeArquivo,
            CaminhoRelativo = pdf.CaminhoRelativo,
            AnoReferencia = 2026,
            TamanhoBytes = pdf.TamanhoBytes,
            HashSha256 = pdf.HashSha256,
            QuantidadePaginas = 0,
            StatusIndexacao = "Pendente",
            CriadoEmUtc = DateTime.UtcNow
        };

        var execucao = new ExecucaoIndexacao
        {
            Id = Guid.NewGuid(),
            DocumentoId = documento.Id,
            Status = "Pendente",
            IniciadaEmUtc = DateTime.UtcNow,
            PaginasProcessadas = 0
        };

        db.Documentos.Add(documento);
        db.ExecucoesIndexacao.Add(execucao);

        await db.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "PDF cadastrado. " +
            "DocumentoId: {DocumentoId} | " +
            "ExecucaoId: {ExecucaoId}",
            documento.Id,
            execucao.Id);

        return documento;
    }

    private async Task IndexarDocumentoAsync(
        RegistroDocDbContext db,
        Documento documento,
        string arquivo,
        CancellationToken cancellationToken)
    {
        var execucao =
            await db.ExecucoesIndexacao
                .Where(item =>
                    item.DocumentoId ==
                    documento.Id)
                .OrderByDescending(item =>
                    item.IniciadaEmUtc)
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (execucao is null)
        {
            execucao = new ExecucaoIndexacao
            {
                Id = Guid.NewGuid(),
                DocumentoId = documento.Id,
                Status = "Pendente",
                IniciadaEmUtc = DateTime.UtcNow,
                PaginasProcessadas = 0
            };

            db.ExecucoesIndexacao.Add(execucao);
        }

        try
        {
            documento.StatusIndexacao =
                "Processando";

            execucao.Status =
                "Processando";

            execucao.MensagemErro =
                null;

            await db.SaveChangesAsync(
                cancellationToken);

            var paginas =
                await _pdfTextExtractor.ExtractAsync(
                    arquivo,
                    cancellationToken);

            var paginasAnteriores =
                await db.PaginasDocumento
                    .Where(item =>
                        item.DocumentoId ==
                        documento.Id)
                    .ToListAsync(
                        cancellationToken);

            if (paginasAnteriores.Count > 0)
            {
                db.PaginasDocumento
                    .RemoveRange(
                        paginasAnteriores);
            }

            foreach (var pagina in paginas)
            {
                db.PaginasDocumento.Add(
                    new PaginaDocumento
                    {
                        Id = Guid.NewGuid(),
                        DocumentoId = documento.Id,
                        NumeroPagina =
                            pagina.NumeroPagina,
                        TextoExtraido =
                            pagina.Texto,
                        ExtraidaEmUtc =
                            DateTime.UtcNow
                    });
            }

            documento.QuantidadePaginas =
                paginas.Count;

            documento.StatusIndexacao =
                "Concluido";

            documento.IndexadoEmUtc =
                DateTime.UtcNow;

            execucao.Status =
                "Concluido";

            execucao.PaginasProcessadas =
                paginas.Count;

            execucao.FinalizadaEmUtc =
                DateTime.UtcNow;

            await db.SaveChangesAsync(
                cancellationToken);

            _logger.LogInformation(
                "PDF indexado. " +
                "DocumentoId: {DocumentoId} | " +
                "Arquivo: {NomeArquivo} | " +
                "Páginas: {QuantidadePaginas}",
                documento.Id,
                documento.NomeArquivo,
                documento.QuantidadePaginas);
        }
        catch (Exception exception)
        {
            documento.StatusIndexacao =
                "Erro";

            execucao.Status =
                "Erro";

            execucao.MensagemErro =
                exception.Message;

            execucao.FinalizadaEmUtc =
                DateTime.UtcNow;

            try
            {
                await db.SaveChangesAsync(
                    CancellationToken.None);
            }
            catch (Exception persistenciaException)
            {
                _logger.LogError(
                    persistenciaException,
                    "Não foi possível registrar o erro de indexação.");
            }

            throw;
        }
    }
}
