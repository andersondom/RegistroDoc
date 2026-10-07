using System.Security.Cryptography;

namespace RegistroDoc.Indexer.Services;

public sealed class PdfFileInspector : IPdfFileInspector
{
    public async Task<PdfFileInfo> InspectAsync(
        string filePath,
        string inputRootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputRootPath);

        var fullPath = Path.GetFullPath(filePath);
        var fullRootPath = Path.GetFullPath(inputRootPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "O arquivo PDF não foi encontrado.",
                fullPath);
        }

        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "O arquivo informado não possui extensão PDF.");
        }

        var relativePath =
            Path.GetRelativePath(fullRootPath, fullPath);

        if (relativePath == ".." ||
            relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException(
                "O PDF está fora da pasta de entrada configurada.");
        }

        var fileInfo = new FileInfo(fullPath);

        if (fileInfo.Length == 0)
        {
            throw new InvalidOperationException(
                "O PDF está vazio.");
        }

        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        var hashBytes =
            await SHA256.HashDataAsync(
                stream,
                cancellationToken);

        var hash =
            Convert.ToHexString(hashBytes);

        return new PdfFileInfo(
            fileInfo.Name,
            fullPath,
            relativePath,
            fileInfo.Length,
            hash);
    }
}
