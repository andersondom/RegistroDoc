namespace RegistroDoc.Api.Services;

public interface IAuditService
{
    Task RegistrarAsync(
        string entidade,
        string entidadeId,
        string acao,
        string? descricao = null,
        CancellationToken cancellationToken = default);
}
