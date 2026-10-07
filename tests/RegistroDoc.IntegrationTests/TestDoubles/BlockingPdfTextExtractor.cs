using RegistroDoc.Indexer.Services;

namespace RegistroDoc.IntegrationTests.TestDoubles;

internal sealed class BlockingPdfTextExtractor
    : IPdfTextExtractor
{
    private readonly TaskCompletionSource<bool>
        _started =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started =>
        _started.Task;

    public async Task<IReadOnlyList<PdfExtractedPage>>
        ExtractAsync(
            string filePath,
            CancellationToken cancellationToken = default)
    {
        _started.TrySetResult(true);

        await Task.Delay(
            Timeout.InfiniteTimeSpan,
            cancellationToken);

        throw new InvalidOperationException(
            "O extrator bloqueante não deveria concluir.");
    }
}