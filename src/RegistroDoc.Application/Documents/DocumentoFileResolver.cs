using System.Security.Cryptography;

namespace RegistroDoc.Application.Documents;

public static class DocumentoFileResolver
{
    public static async Task<string?> ResolveAsync(
        string rootPath, string relativePath, int? year, string hash,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(rootPath);
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var direct = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!direct.StartsWith(prefix, comparison))
            throw new ArgumentException("Caminho de documento inválido.");

        var candidates = new List<string> { direct };
        // Compatibilidade com indexadores antigos do Compose, sem regravar metadados.
        if (year.HasValue && Path.GetFileName(relativePath) == relativePath)
        {
            var folder = year.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            candidates.Add(Path.Combine(root, folder, relativePath));
            candidates.Add(Path.Combine(root, "benchmark", folder, relativePath));
        }

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate)) continue;
            await using var stream = File.OpenRead(candidate);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (string.Equals(actual, hash, StringComparison.OrdinalIgnoreCase)) return candidate;
        }
        return null;
    }
}
