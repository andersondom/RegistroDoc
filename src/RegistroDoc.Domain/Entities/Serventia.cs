namespace RegistroDoc.Domain.Entities;

public class Serventia
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Nome { get; set; } = string.Empty;

    public string CodigoCns { get; set; } = string.Empty;

    public string Municipio { get; set; } = string.Empty;

    public string Uf { get; set; } = string.Empty;

    public bool Ativa { get; set; } = true;

    public DateTime CriadaEmUtc { get; set; } = DateTime.UtcNow;
}
