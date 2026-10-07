namespace RegistroDoc.Indexer.Services;

public sealed record PdfFileInfo(
    string NomeArquivo,
    string CaminhoCompleto,
    string CaminhoRelativo,
    long TamanhoBytes,
    string HashSha256);
