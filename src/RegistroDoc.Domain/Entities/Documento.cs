namespace RegistroDoc.Domain.Entities;

public class Documento
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ServentiaId { get; set; }

    public string TipoDocumento { get; set; } = "HabilitacaoCasamento";

    public string NomeArquivo { get; set; } = string.Empty;

    public string CaminhoRelativo { get; set; } = string.Empty;

    public int? AnoReferencia { get; set; }

    public long TamanhoBytes { get; set; }

    public string HashSha256 { get; set; } = string.Empty;

    public int QuantidadePaginas { get; set; }

    public string StatusIndexacao { get; set; } = "Pendente";

    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;

    public DateTime? IndexadoEmUtc { get; set; }

    public Serventia? Serventia { get; set; }
}
