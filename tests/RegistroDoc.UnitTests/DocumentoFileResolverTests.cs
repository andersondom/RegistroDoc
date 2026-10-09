using System.Security.Cryptography;
using RegistroDoc.Application.Documents;

namespace RegistroDoc.UnitTests;

public sealed class DocumentoFileResolverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeveRejeitarLinkParaArquivoOuPastaForaDaRaiz(bool directoryLink)
    {
        // Links simbólicos no Windows podem exigir privilégio; cobertura executada no Docker Linux.
        if (OperatingSystem.IsWindows()) return;
        var temporary = Path.Combine(Path.GetTempPath(), "registrodoc-links-" + Guid.NewGuid());
        var root = Path.Combine(temporary, "root");
        var outside = Path.Combine(temporary, "outside");
        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(outside);
            byte[] bytes = "%PDF-1.4 ficticio externo"u8.ToArray();
            var file = Path.Combine(outside, "ficticio.pdf");
            await File.WriteAllBytesAsync(file, bytes);
            var relative = directoryLink ? "link/ficticio.pdf" : "link.pdf";
            if (directoryLink)
                Directory.CreateSymbolicLink(Path.Combine(root, "link"), outside);
            else
                File.CreateSymbolicLink(Path.Combine(root, "link.pdf"), file);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                DocumentoFileResolver.ResolveAsync(root, relative, null, hash));
        }
        finally
        {
            // Área temporária exclusiva; exclusão não segue links simbólicos no Linux.
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    [Theory]
    [InlineData("2026")]
    [InlineData("benchmark/2026")]
    [InlineData("")]
    public async Task DeveResolverPdfCorretoSemAlterarMetadados(string folder)
    {
        var root = Path.Combine(Path.GetTempPath(), "registrodoc-test-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, folder));
            var file = Path.Combine(root, folder, "ficticio.pdf");
            byte[] bytes = "%PDF-1.4 ficticio"u8.ToArray();
            await File.WriteAllBytesAsync(file, bytes);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            Assert.Equal(Path.GetFullPath(file), await DocumentoFileResolver.ResolveAsync(root, "ficticio.pdf", 2026, hash));
            Assert.Null(await DocumentoFileResolver.ResolveAsync(root, "ficticio.pdf", 2026, new string('0', 64)));
            await Assert.ThrowsAsync<ArgumentException>(() => DocumentoFileResolver.ResolveAsync(root, "../fora.pdf", 2026, hash));
        }
        finally
        {
            // Apenas diretório temporário exclusivo criado por este teste.
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
