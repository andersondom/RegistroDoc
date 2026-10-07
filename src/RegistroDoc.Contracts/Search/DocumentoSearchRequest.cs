namespace RegistroDoc.Contracts.Search;

public sealed class DocumentoSearchRequest
{
    public string? Termo { get; init; }
    public string? TipoDocumento { get; init; }
    public int? AnoReferencia { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanhoPagina { get; init; } = 20;
}
