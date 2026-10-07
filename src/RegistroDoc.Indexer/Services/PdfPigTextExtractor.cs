using UglyToad.PdfPig;

namespace RegistroDoc.Indexer.Services;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public Task<IReadOnlyList<PdfExtractedPage>> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "O arquivo PDF não foi encontrado.",
                filePath);
        }

        return Task.Run<IReadOnlyList<PdfExtractedPage>>(
            () =>
            {
                var paginas =
                    new List<PdfExtractedPage>();

                using var documento =
                    PdfDocument.Open(filePath);

                foreach (var pagina in documento.GetPages())
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    paginas.Add(
                        new PdfExtractedPage(
                            pagina.Number,
                            pagina.Text ?? string.Empty));
                }

                return paginas;
            },
            cancellationToken);
    }
}
