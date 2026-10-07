using RegistroDoc.Api.Security;
using RegistroDoc.Domain.Entities;
using RegistroDoc.Infrastructure.Persistence;

namespace RegistroDoc.Api.Services;

public sealed class AuditService : IAuditService
{
    private readonly RegistroDocDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public AuditService(
        RegistroDocDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task RegistrarAsync(
        string entidade,
        string entidadeId,
        string acao,
        string? descricao = null,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated ||
            string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            throw new InvalidOperationException(
                "Não é possível registrar auditoria sem usuário autenticado.");
        }

        var auditoria = new Auditoria
        {
            Id = Guid.NewGuid(),
            Entidade = entidade,
            EntidadeId = entidadeId,
            Acao = acao,
            UsuarioId = _currentUser.UserId,
            Descricao = descricao,
            OcorridaEmUtc = DateTime.UtcNow
        };

        _dbContext.Auditorias.Add(auditoria);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
