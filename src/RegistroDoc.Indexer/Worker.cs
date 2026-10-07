using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RegistroDoc.Domain.Entities;
using RegistroDoc.Indexer.Options;
using RegistroDoc.Indexer.Services;
using RegistroDoc.Infrastructure.Persistence;

namespace RegistroDoc.Indexer;

public sealed class Worker : BackgroundService
{
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
                string.IsNullOrWhiteSpace(_options.RepositoryRootPath)
                    ? inputPath
                    : Path.GetFullPath(_options.RepositoryRootPath),
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
            try
            {
                documento =
                    await CriarDocumentoAsync(
                        db,
                        pdf,
                        cancellationToken);
            }
            catch (DbUpdateException exception)
                when (
                    exception.InnerException
                        is PostgresException postgresException &&
                    postgresException.SqlState ==
                        PostgresErrorCodes.UniqueViolation &&
                    postgresException.ConstraintName ==
                        "IX_Documentos_HashSha256")
            {
                _logger.LogInformation(
                    "Concorrência detectada para SHA-256 {HashSha256}. " +
                    "Outro Worker cadastrou o documento primeiro.",
                    pdf.HashSha256);

                db.ChangeTracker.Clear();

                documento =
                    await db.Documentos
                        .FirstOrDefaultAsync(
                            item =>
                                item.HashSha256 ==
                                pdf.HashSha256,
                            cancellationToken);

                if (documento is null)
                {
                    throw new InvalidOperationException(
                        "Violação de unicidade detectada, " +
                        "mas o documento concorrente não foi localizado.",
                        exception);
                }
            }
        }

        if (
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
                        _options.CodigoCnsServentia,
                    cancellationToken);

        if (serventia is null)
        {
            throw new InvalidOperationException(
                $"Serventia fictícia {_options.CodigoCnsServentia} não encontrada.");
        }

        var documento = new Documento
        {
            Id = Guid.NewGuid(),
            ServentiaId = serventia.Id,
            TipoDocumento = _options.TipoDocumento,
            NomeArquivo = pdf.NomeArquivo,
            CaminhoRelativo = pdf.CaminhoRelativo,
            AnoReferencia = _options.AnoReferencia,
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

        var processamentoAdquirido = false;


        try
        {
            var agoraUtc =
                DateTime.UtcNow;

            var limiteProcessamentoUtc =
                agoraUtc.AddMinutes(
                    -_options.ProcessingTimeoutMinutes);

            var documentosAdquiridos =
                await db.Documentos
                    .Where(item =>
                        item.Id == documento.Id &&
                        (
                            item.StatusIndexacao == "Pendente" ||
                            item.StatusIndexacao == "Erro"
                        ))
                    .ExecuteUpdateAsync(
                        setters =>
                            setters.SetProperty(
                                item =>
                                    item.StatusIndexacao,
                                "Processando"),
                        cancellationToken);

            var recuperado =
                false;

            if (
                documentosAdquiridos == 0 &&
                string.Equals(
                    documento.StatusIndexacao,
                    "Processando",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    execucao.Status,
                    "Processando",
                    StringComparison.OrdinalIgnoreCase))
            {
                var execucoesRecuperadas =
                    await db.ExecucoesIndexacao
                        .Where(item =>
                            item.Id == execucao.Id &&
                            item.Status == "Processando" &&
                            item.IniciadaEmUtc <=
                                limiteProcessamentoUtc)
                        .ExecuteUpdateAsync(
                            setters =>
                                setters
                                    .SetProperty(
                                        item =>
                                            item.IniciadaEmUtc,
                                        agoraUtc)
                                    .SetProperty(
                                        item =>
                                            item.FinalizadaEmUtc,
                                        (DateTime?)null)
                                    .SetProperty(
                                        item =>
                                            item.MensagemErro,
                                        (string?)null),
                            cancellationToken);

                recuperado =
                    execucoesRecuperadas == 1;

                if (recuperado)
                {
                    _logger.LogWarning(
                        "Processamento abandonado recuperado. " +
                        "DocumentoId: {DocumentoId} | " +
                        "ExecucaoId: {ExecucaoId} | " +
                        "Arquivo: {NomeArquivo} | " +
                        "TimeoutMinutos: {TimeoutMinutos}",
                        documento.Id,
                        execucao.Id,
                        documento.NomeArquivo,
                        _options.ProcessingTimeoutMinutes);
                }
            }

            if (
                documentosAdquiridos == 0 &&
                !recuperado)
            {
                _logger.LogInformation(
                    "Documento não adquirido para indexação. " +
                    "Outro Worker já iniciou ou concluiu o processamento. " +
                    "DocumentoId: {DocumentoId} | " +
                    "Arquivo: {NomeArquivo}",
                    documento.Id,
                    documento.NomeArquivo);

                return;
            }

            processamentoAdquirido = true;

            if (!recuperado)
            {
                _logger.LogInformation(
                    "Documento adquirido para indexação. " +
                    "DocumentoId: {DocumentoId} | " +
                    "Arquivo: {NomeArquivo}",
                    documento.Id,
                    documento.NomeArquivo);
            }


            documento.StatusIndexacao =
                "Processando";

            execucao.Status =
                "Processando";

            execucao.IniciadaEmUtc =
                DateTime.UtcNow;

            execucao.FinalizadaEmUtc =
                null;

            execucao.MensagemErro =
                null;

            await db.SaveChangesAsync(
                cancellationToken);

            var paginas =
                await _pdfTextExtractor.ExtractAsync(
                    arquivo,
                    cancellationToken);

            await using var transaction =
                await db.Database.BeginTransactionAsync(
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

            await transaction.CommitAsync(
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
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Indexação cancelada durante o encerramento. " +
                "DocumentoId: {DocumentoId} | " +
                "Arquivo: {NomeArquivo}",
                documento.Id,
                documento.NomeArquivo);

            if (processamentoAdquirido)
            {
                try
                {
                    await db.Documentos
                        .Where(item =>
                            item.Id == documento.Id &&
                            item.StatusIndexacao == "Processando")
                        .ExecuteUpdateAsync(
                            setters =>
                                setters.SetProperty(
                                    item =>
                                        item.StatusIndexacao,
                                    "Pendente"),
                            CancellationToken.None);

                    await db.ExecucoesIndexacao
                        .Where(item =>
                            item.Id == execucao.Id &&
                            item.Status == "Processando")
                        .ExecuteUpdateAsync(
                            setters =>
                                setters
                                    .SetProperty(
                                        item =>
                                            item.Status,
                                        "Pendente")
                                    .SetProperty(
                                        item =>
                                            item.FinalizadaEmUtc,
                                        (DateTime?)null)
                                    .SetProperty(
                                        item =>
                                            item.MensagemErro,
                                        (string?)null),
                            CancellationToken.None);

                    _logger.LogInformation(
                        "Aquisição liberada após cancelamento. " +
                        "DocumentoId: {DocumentoId} | " +
                        "ExecucaoId: {ExecucaoId}",
                        documento.Id,
                        execucao.Id);
                }
                catch (Exception liberacaoException)
                {
                    _logger.LogError(
                        liberacaoException,
                        "Não foi possível liberar a aquisição " +
                        "após o cancelamento. " +
                        "DocumentoId: {DocumentoId}",
                        documento.Id);
                }
            }

            throw;
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
