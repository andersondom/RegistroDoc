using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RegistroDoc.Indexer.Services;

namespace RegistroDoc.Indexer.Importing;

public sealed class ImportPlanException : Exception
{
    public ImportPlanException() : base("Gere e revise uma nova simulação antes da carga.") { }
}

public sealed record ImportReportHeader(string Registro, int Versao, string Modo,
    string ConfiguracaoSha256, DateTime GeradoEmUtc);

internal static class ImportPlanReview
{
    public static ImportReportHeader Header(ImportOptions options, bool execute) =>
        new("RegistroDoc.Importacao", 1, execute ? "Execucao" : "Simulacao", Fingerprint(options), DateTime.UtcNow);

    private static string Fingerprint(ImportOptions options)
    {
        var configuration = JsonSerializer.Serialize(new
        {
            Source = Path.GetFullPath(options.SourcePath),
            Target = Path.GetFullPath(options.TargetRootPath),
            options.CodigoCnsServentia,
            options.DatabaseIdentity,
            options.ExecutionContext,
            Categories = options.Categorias.OrderBy(c => c.Key, StringComparer.Ordinal)
                .Select(c => new { Folder = c.Key.Replace('\\', '/').Trim('/'), c.Value.TipoDocumento,
                    c.Value.AnoReferencia, c.Value.AnoPorSubpasta }).ToArray()
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configuration)));
    }

    public static async Task<IReadOnlyDictionary<string, ImportItem>> ValidateAsync(
        ImportOptions options, IReadOnlyList<string> files, Func<string, ImportCategory> resolveCategory,
        IPdfFileInspector inspector, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ApprovedPlanPath)) throw new ImportPlanException();
        var approvedPath = Path.GetFullPath(options.ApprovedPlanPath);
        foreach (var root in new[] { options.SourcePath, options.TargetRootPath })
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(root), approvedPath);
            if (!Path.IsPathRooted(relative) && relative != ".." &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new ImportPlanException();
        }
        var records = new Dictionary<string, ImportItem>(StringComparer.Ordinal);
        ImportSummary? summary = null;
        try
        {
            using var reader = new StreamReader(approvedPath);
            var first = await reader.ReadLineAsync(cancellationToken);
            if (first is null) throw new ImportPlanException();
            var header = JsonSerializer.Deserialize<ImportReportHeader>(first);
            if (header is null || header.Registro != "RegistroDoc.Importacao" || header.Versao != 1 ||
                header.Modo != "Simulacao" || header.ConfiguracaoSha256 != Fingerprint(options))
                throw new ImportPlanException();
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (summary is not null || string.IsNullOrWhiteSpace(line)) throw new ImportPlanException();
                using var json = JsonDocument.Parse(line);
                if (json.RootElement.TryGetProperty("Estado", out _))
                {
                    var item = json.Deserialize<ImportItem>();
                    if (item is null || string.IsNullOrWhiteSpace(item.CaminhoRelativo) || item.Paginas < 1 ||
                        item.HashSha256 is null || item.HashSha256.Length != 64 ||
                        !item.HashSha256.All(Uri.IsHexDigit) || item.Estado is not ("Pronto" or "DuplicadoNoLote") ||
                        !records.TryAdd(item.CaminhoRelativo, item)) throw new ImportPlanException();
                }
                else if (json.RootElement.TryGetProperty("Total", out _)) summary = json.Deserialize<ImportSummary>();
                else throw new ImportPlanException();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { throw new ImportPlanException(); }

        if (summary is null || summary.Falhas != 0 || summary.Importados != 0 || summary.Prontos < 1 ||
            summary.Total != records.Count || summary.Total != files.Count ||
            summary.Prontos != records.Values.Count(r => r.Estado == "Pronto") ||
            summary.Duplicados != records.Values.Count(r => r.Estado == "DuplicadoNoLote"))
            throw new ImportPlanException();

        // Verifica todo o lote antes de abrir conexão ou criar qualquer arquivo no destino.
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(options.SourcePath, file).Replace('\\', '/');
            if (!records.TryGetValue(relative, out var approved)) throw new ImportPlanException();
            var category = resolveCategory(relative);
            var info = await inspector.InspectAsync(file, options.SourcePath, cancellationToken);
            if (info.HashSha256 != approved.HashSha256 || category.TipoDocumento != approved.TipoDocumento ||
                category.AnoReferencia != approved.AnoReferencia) throw new ImportPlanException();
        }
        return records;
    }
}
