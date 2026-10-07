namespace RegistroDoc.Indexer.Options;

public sealed class IndexerOptions
{
    public const string SectionName = "Indexer";

    public string InputPath { get; set; } = string.Empty;

    public string SearchPattern { get; set; } = "*.pdf";

    public bool IncludeSubdirectories { get; set; } = true;

    public int ScanIntervalSeconds { get; set; } = 10;

    public string CodigoCnsServentia { get; set; } = string.Empty;

    public string TipoDocumento { get; set; } = "HabilitacaoCasamento";

    public int? AnoReferencia { get; set; }
}
