namespace RegistroDoc.Domain.Entities;

public class PaginaDocumento
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentoId { get; set; }

    public int NumeroPagina { get; set; }

    public string TextoExtraido { get; set; } = string.Empty;

    public DateTime ExtraidaEmUtc { get; set; } = DateTime.UtcNow;

    public Documento? Documento { get; set; }
}
