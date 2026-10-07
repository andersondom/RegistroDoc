namespace RegistroDoc.Contracts.Search;

public sealed record DocumentoSearchResponse(
    IReadOnlyList<DocumentoSearchItem> Itens,
    int Pagina,
    int TamanhoPagina,
    int TotalItens,
    int TotalPaginas);
