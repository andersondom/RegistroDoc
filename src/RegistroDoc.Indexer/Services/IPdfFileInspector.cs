namespace RegistroDoc.Indexer.Services;

public interface IPdfFileInspector
{
    Task<PdfFileInfo> InspectAsync(
        string filePath,
        string inputRootPath,
        CancellationToken cancellationToken = default);
}
