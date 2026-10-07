namespace RegistroDoc.Contracts.Search;

public sealed record DocumentoSearchItem(
    Guid DocumentoId,
    string NomeArquivo,
    string TipoDocumento,
    int? AnoReferencia,
    int QuantidadePaginas,
    DateTime? IndexadoEmUtc,
    IReadOnlyList<int> PaginasEncontradas,
    string? Trecho);
