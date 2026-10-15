using RegistroDoc.Api.Security;
using RegistroDoc.Domain.Entities;
using RegistroDoc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace RegistroDoc.Api.Services;

public sealed class AuditService : IAuditService
{
    public async Task<Guid> IniciarAcessoAsync(string acao, Guid? documentoId, bool parcial,
        CancellationToken cancellationToken = default)
    {
        var usuario = _currentUser.IsAuthenticated ? _currentUser.UserId : null;
        if (_currentUser.IsAuthenticated && (string.IsNullOrWhiteSpace(usuario) || usuario.Length > 100))
            throw new InvalidOperationException("Identidade de auditoria inválida.");
        var registro = new Auditoria
        {
            Entidade = "AcessoDocumento", EntidadeId = documentoId?.ToString() ?? "Pesquisa",
            Acao = acao, UsuarioId = usuario,
            Descricao = JsonSerializer.Serialize(new { Versao = 1, Resultado = "Iniciado",
                StatusHttp = (int?)null, Parcial = parcial }),
            OcorridaEmUtc = DateTime.UtcNow
        };
        _dbContext.Auditorias.Add(registro);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.Entry(registro).State = EntityState.Detached;
        return registro.Id;
    }

    public async Task ConcluirAcessoAsync(Guid id, int? statusHttp, string resultado,
        CancellationToken cancellationToken = default)
    {
        var registro = await _dbContext.Auditorias.AsNoTracking().SingleAsync(a => a.Id == id, cancellationToken);
        using var inicio = JsonDocument.Parse(registro.Descricao!);
        var descricao = JsonSerializer.Serialize(new { Versao = 1, Resultado = resultado,
            StatusHttp = statusHttp, Parcial = inicio.RootElement.GetProperty("Parcial").GetBoolean() });
        var count = await _dbContext.Auditorias.Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Descricao, descricao), cancellationToken);
        if (count != 1) throw new InvalidOperationException("Registro de auditoria ausente.");
    }

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
