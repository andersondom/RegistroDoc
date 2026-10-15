using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegistroDoc.Contracts.Security;
using RegistroDoc.Infrastructure.Persistence;

namespace RegistroDoc.Api.Controllers;

[ApiController, Route("api/auditorias"), Authorize(Roles = AccessRoles.Administrador)]
public sealed class AuditoriasController(RegistroDocDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int pagina = 1, CancellationToken cancellationToken = default)
    {
        if (pagina < 1 || pagina > 100000) return BadRequest();
        Response.Headers.CacheControl = "no-store";
        return Ok(await db.Auditorias.AsNoTracking().Where(a => a.Entidade == "AcessoDocumento")
            .OrderByDescending(a => a.OcorridaEmUtc).ThenBy(a => a.Id).Skip((pagina - 1) * 50).Take(50)
            .Select(a => new { a.Id, a.UsuarioId, a.Acao, a.EntidadeId, a.OcorridaEmUtc, a.Descricao })
            .ToListAsync(cancellationToken));
    }
}
