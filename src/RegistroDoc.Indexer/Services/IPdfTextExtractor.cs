namespace RegistroDoc.Indexer.Services;

public interface IPdfTextExtractor
{
    Task<IReadOnlyList<PdfExtractedPage>> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
