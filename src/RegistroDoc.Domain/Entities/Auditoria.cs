namespace RegistroDoc.Domain.Entities;

public class Auditoria
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Entidade { get; set; } = string.Empty;

    public string EntidadeId { get; set; } = string.Empty;

    public string Acao { get; set; } = string.Empty;

    public string? UsuarioId { get; set; }

    public string? Descricao { get; set; }

    public DateTime OcorridaEmUtc { get; set; } = DateTime.UtcNow;
}
