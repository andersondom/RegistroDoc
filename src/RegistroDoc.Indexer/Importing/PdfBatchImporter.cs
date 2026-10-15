using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RegistroDoc.Indexer.Options;
using RegistroDoc.Indexer.Services;
using RegistroDoc.Infrastructure.Persistence;
using RegistroDoc.Application.Documents;

namespace RegistroDoc.Indexer.Importing;

public sealed class PdfBatchImporter(
    IPdfFileInspector inspector, IPdfTextExtractor extractor,
    IServiceScopeFactory scopeFactory, IHostEnvironment environment)
{
    public async Task<ImportSummary> RunAsync(ImportOptions options, bool execute,
        CancellationToken cancellationToken = default)
    {
        Validate(options);
        var source = Path.GetFullPath(options.SourcePath);
        var target = Path.GetFullPath(options.TargetRootPath);
        var reportDirectory = Path.GetFullPath(options.ReportDirectory);
        if (!Directory.Exists(source)) throw new ArgumentException("Pasta de origem não encontrada.");
        RejectLinks(source, source);
        var categories = options.Categorias.Select(pair => (
            Prefix: NormalizeFolder(pair.Key), Category: pair.Value)).OrderByDescending(c => c.Prefix.Length).ToArray();
        var enumeration = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint };
        var files = Directory.EnumerateFiles(source, "*", enumeration)
            .Where(f => Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => Path.GetRelativePath(source, f).Replace('\\', '/'), StringComparer.Ordinal).ToArray();
        IReadOnlyDictionary<string, ImportItem>? approved = null;
        if (execute)
            approved = await ImportPlanReview.ValidateAsync(options, files,
                relative => ResolveCategory(relative, categories), inspector, cancellationToken);
        if (execute)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RegistroDocDbContext>();
            if (!await db.Serventias.AnyAsync(s => s.CodigoCns == options.CodigoCnsServentia && s.Ativa, cancellationToken))
                throw new ArgumentException("Serventia ativa não encontrada; nenhuma carga iniciada.");
            Directory.CreateDirectory(target);
            RejectLinks(target, target);
        }
        Directory.CreateDirectory(reportDirectory);
        var report = Path.Combine(reportDirectory, $"importacao-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.ndjson");
        await using var writer = new StreamWriter(new FileStream(report, FileMode.CreateNew, FileAccess.Write));
        await writer.WriteLineAsync(JsonSerializer.Serialize(ImportPlanReview.Header(options, execute)));
        await writer.FlushAsync(cancellationToken);
        var hashes = new Dictionary<string, (string Type, int? Year)>(StringComparer.Ordinal);
        int total = 0, ready = 0, imported = 0, duplicates = 0, failures = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total++;
            var relative = Path.GetRelativePath(source, file).Replace('\\', '/');
            ImportCategory? category = null;
            ImportItem item;
            try
            {
                category = ResolveCategory(relative, categories);
                RejectLinks(source, file);
                var info = await inspector.InspectAsync(file, source, cancellationToken);
                var pages = await extractor.ExtractAsync(file, cancellationToken);
                if (pages.Count == 0 || !pages.Any(p => !string.IsNullOrWhiteSpace(p.Texto)))
                    throw new InvalidDataException("PDF sem texto pesquisável.");
                if (approved is not null && (approved[relative].HashSha256 != info.HashSha256 ||
                    approved[relative].Paginas != pages.Count)) throw new ImportPlanException();
                var state = "Pronto";
                if (hashes.TryGetValue(info.HashSha256, out var previous))
                    state = previous == (category.TipoDocumento, category.AnoReferencia) ? "DuplicadoNoLote" : "ConflitoMetadados";
                else
                {
                    hashes.Add(info.HashSha256, (category.TipoDocumento, category.AnoReferencia));
                    if (execute) state = await ImportAsync(options, category, info, pages, target, cancellationToken);
                }
                item = new(relative, category.TipoDocumento, category.AnoReferencia, info.HashSha256, pages.Count, state);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                var state = exception is MissingYearException ? "AnoNaoIdentificado"
                    : exception is ImportPlanException ? "PlanoDivergente"
                    : category is null ? "CategoriaNaoMapeada"
                    : exception is InvalidDataException ? "SemTextoPesquisavel" : "FalhaLeituraOuCarga";
                item = new(relative, category?.TipoDocumento, category?.AnoReferencia, null, 0, state);
            }
            switch (item.Estado)
            {
                case "Pronto": ready++; break;
                case "Importado": imported++; break;
                case "DuplicadoNoLote": case "JaIndexado": duplicates++; break;
                default: failures++; break;
            }
            // Sem texto extraído, credenciais ou mensagens internas no relatório.
            await writer.WriteLineAsync(JsonSerializer.Serialize(item));
            await writer.FlushAsync(cancellationToken);
        }
        var summary = new ImportSummary(total, ready, imported, duplicates, failures);
        await writer.WriteLineAsync(JsonSerializer.Serialize(summary));
        return summary;
    }

    private sealed class MissingYearException : Exception { }

    private static ImportCategory ResolveCategory(string relative,
        (string Prefix, ImportCategory Category)[] categories)
    {
        var mapping = categories.FirstOrDefault(c => relative.StartsWith(c.Prefix + "/",
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        var category = mapping.Category ?? throw new ArgumentException("Categoria não mapeada.");
        if (!category.AnoPorSubpasta) return category;
        var remainder = relative[(mapping.Prefix.Length + 1)..].Split('/');
        if (remainder.Length < 2 || remainder[0].Length != 4 ||
            !int.TryParse(remainder[0], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var year) || year is < 1 or > 9999)
            throw new MissingYearException();
        return new ImportCategory { TipoDocumento = category.TipoDocumento, AnoReferencia = year };
    }

    private async Task<string> ImportAsync(ImportOptions options, ImportCategory category,
        PdfFileInfo pdf, IReadOnlyList<PdfExtractedPage> pages, string target, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RegistroDocDbContext>();
        var existing = await db.Documentos.AsNoTracking().Include(d => d.Serventia)
            .SingleOrDefaultAsync(d => d.HashSha256 == pdf.HashSha256, cancellationToken);
        if (existing is not null)
        {
            if (existing.Serventia?.CodigoCns != options.CodigoCnsServentia ||
                existing.TipoDocumento != category.TipoDocumento || existing.AnoReferencia != category.AnoReferencia)
                return "ConflitoMetadados";
            if (existing.StatusIndexacao == "Concluido")
                return await DocumentoFileResolver.ResolveAsync(target, existing.CaminhoRelativo,
                    existing.AnoReferencia, existing.HashSha256, cancellationToken) is not null
                    ? "JaIndexado" : "ArquivoIndexadoIndisponivel";
        }
        var destination = Path.Combine(target, "importacao", pdf.HashSha256[..2], pdf.HashSha256, pdf.NomeArquivo);
        if (existing is not null && !string.Equals(existing.CaminhoRelativo,
            Path.GetRelativePath(target, destination).Replace('\\', '/'), StringComparison.Ordinal)) return "ConflitoCaminho";
        CreatePlainDirectories(target, Path.GetDirectoryName(destination)!);
        if (!File.Exists(destination))
        {
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var input = File.OpenRead(pdf.CaminhoCompleto))
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                    await input.CopyToAsync(output, cancellationToken);
                await VerifyHashAsync(temporary, pdf.HashSha256, cancellationToken);
                try { File.Move(temporary, destination, overwrite: false); }
                catch (IOException) when (File.Exists(destination)) { /* Outra execução publicou; verificar abaixo. */ }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        RejectLinks(target, destination);
        await VerifyHashAsync(destination, pdf.HashSha256, cancellationToken);
        var workerOptions = new IndexerOptions { InputPath = target, CodigoCnsServentia = options.CodigoCnsServentia,
            TipoDocumento = category.TipoDocumento, AnoReferencia = category.AnoReferencia };
        using var worker = new Worker(NullLogger<Worker>.Instance, Microsoft.Extensions.Options.Options.Create(workerOptions),
            environment, inspector, new PreparedTextExtractor(pages), scopeFactory);
        await worker.ImportFileAsync(destination, target, cancellationToken);
        var registered = await db.Documentos.AsNoTracking().Include(d => d.Serventia)
            .SingleOrDefaultAsync(d => d.HashSha256 == pdf.HashSha256, cancellationToken);
        if (registered is not null && (registered.TipoDocumento != category.TipoDocumento ||
            registered.AnoReferencia != category.AnoReferencia || registered.Serventia?.CodigoCns != options.CodigoCnsServentia))
            return "ConflitoMetadados";
        return registered?.StatusIndexacao == "Concluido" ? "Importado" : "IndexacaoPendente";
    }

    private sealed class PreparedTextExtractor(IReadOnlyList<PdfExtractedPage> pages) : IPdfTextExtractor
    {
        public Task<IReadOnlyList<PdfExtractedPage>> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(pages);
        }
    }

    private static async Task VerifyHashAsync(string file, string expected, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(file);
        if (Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)) != expected)
            throw new IOException("Arquivo alterado ou cópia incompleta.");
    }

    private static string NormalizeFolder(string folder) => folder.Replace('\\', '/').Trim('/');

    private static void Validate(ImportOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TargetRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.CodigoCnsServentia);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ReportDirectory);
        var source = Path.GetFullPath(options.SourcePath);
        var target = Path.GetFullPath(options.TargetRootPath);
        var report = Path.GetFullPath(options.ReportDirectory);
        RejectLinkedAncestors(source);
        RejectLinkedAncestors(target);
        RejectLinkedAncestors(report);
        if (Contains(source, target) || Contains(target, source) || Contains(source, report) || Contains(target, report))
            throw new ArgumentException("Origem, destino e relatórios precisam de pastas separadas.");
        if (options.Categorias.Count == 0) throw new ArgumentException("Mapeie ao menos uma categoria.");
        foreach (var pair in options.Categorias)
        {
            var parts = NormalizeFolder(pair.Key).Split('/');
            if (Path.IsPathRooted(pair.Key) || parts.Any(p => p is "" or "." or ".." || p.Contains(':')) ||
                string.IsNullOrWhiteSpace(pair.Value.TipoDocumento) || pair.Value.TipoDocumento.Length > 50 ||
                (pair.Value.AnoPorSubpasta && pair.Value.AnoReferencia.HasValue) ||
                pair.Value.AnoReferencia is < 1 or > 9999)
                throw new ArgumentException("Mapeamento de categoria inválido.");
        }
    }

    private static bool Contains(string parent, string child)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(parent.TrimEnd(Path.DirectorySeparatorChar), child.TrimEnd(Path.DirectorySeparatorChar), comparison) ||
            child.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison);
    }

    private static void RejectLinkedAncestors(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("As raízes não podem utilizar links ou junções.");
    }

    private static void RejectLinks(string root, string path)
    {
        if (!Contains(root, Path.GetFullPath(path))) throw new IOException("Caminho fora da raiz.");
        FileSystemInfo current = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        while (true)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Link não permitido.");
            if (string.Equals(current.FullName.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return;
            current = current is FileInfo file ? file.Directory! : ((DirectoryInfo)current).Parent!;
            if (current is null) throw new IOException("Caminho fora da raiz.");
        }
    }

    private static void CreatePlainDirectories(string root, string directory)
    {
        var current = root;
        RejectLinks(root, root);
        foreach (var part in Path.GetRelativePath(root, directory).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            if (!Directory.Exists(current)) Directory.CreateDirectory(current);
            RejectLinks(root, current);
        }
    }
}
