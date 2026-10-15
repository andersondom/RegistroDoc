namespace RegistroDoc.Api.Services;

public interface IAuditService
{
    Task<Guid> IniciarAcessoAsync(string acao, Guid? documentoId, bool parcial,
        CancellationToken cancellationToken = default);
    Task ConcluirAcessoAsync(Guid id, int? statusHttp, string resultado,
        CancellationToken cancellationToken = default);

    Task RegistrarAsync(
        string entidade,
        string entidadeId,
        string acao,
        string? descricao = null,
        CancellationToken cancellationToken = default);
}
