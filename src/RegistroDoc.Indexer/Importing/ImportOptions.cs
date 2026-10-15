namespace RegistroDoc.Indexer.Importing;

public sealed class ImportOptions
{
    public string SourcePath { get; set; } = "";
    public string TargetRootPath { get; set; } = "";
    public string CodigoCnsServentia { get; set; } = "";
    public string ReportDirectory { get; set; } = "artifacts/importacao";
    public string ApprovedPlanPath { get; set; } = "";
    public string DatabaseIdentity { get; set; } = "";
    public string ExecutionContext { get; set; } = "";
    public Dictionary<string, ImportCategory> Categorias { get; set; } = new();
}

public sealed class ImportCategory
{
    public string TipoDocumento { get; set; } = "";
    public int? AnoReferencia { get; set; }
    public bool AnoPorSubpasta { get; set; }
}

public sealed record ImportItem(string CaminhoRelativo, string? TipoDocumento,
    int? AnoReferencia, string? HashSha256, int Paginas, string Estado);

public sealed record ImportSummary(int Total, int Prontos, int Importados,
    int Duplicados, int Falhas);
