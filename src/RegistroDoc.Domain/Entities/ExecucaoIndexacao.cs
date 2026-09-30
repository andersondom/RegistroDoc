namespace RegistroDoc.Domain.Entities;

public class ExecucaoIndexacao
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentoId { get; set; }

    public string Status { get; set; } = "Pendente";

    public DateTime IniciadaEmUtc { get; set; } = DateTime.UtcNow;

    public DateTime? FinalizadaEmUtc { get; set; }

    public int PaginasProcessadas { get; set; }

    public string? MensagemErro { get; set; }

    public Documento? Documento { get; set; }
}
