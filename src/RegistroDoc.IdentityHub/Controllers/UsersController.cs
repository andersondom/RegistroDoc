using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegistroDoc.Contracts.Security;
using RegistroDoc.IdentityHub.Data;

namespace RegistroDoc.IdentityHub.Controllers;

[ApiController, Route("api/users"), Authorize(Roles = AccessRoles.Administrador)]
public sealed class UsersController(UserManager<IdentityHubUser> users, IdentityHubDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int pagina = 1)
    {
        if (pagina < 1 || pagina > 100000) return BadRequest();
        Response.Headers.CacheControl = "no-store";
        var records = await users.Users.OrderBy(u => u.Email).Skip((pagina - 1) * 50).Take(50).ToListAsync();
        var result = new List<UserAccount>();
        foreach (var user in records) result.Add(new(user.Id, user.Email!, user.NomeCompleto, user.Ativo,
            (await users.GetRolesAsync(user)).ToArray()));
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAccount request)
    {
        if (!AccessRoles.Todos.Contains(request.Perfil)) return BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = new IdentityHubUser { UserName = request.Email.Trim(), Email = request.Email.Trim(),
            NomeCompleto = request.NomeCompleto.Trim(), EmailConfirmed = true };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return InvalidAccount();
        result = await users.AddToRoleAsync(user, request.Perfil);
        if (!result.Succeeded) return InvalidAccount();
        await tx.CommitAsync();
        return StatusCode(201, new { user.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateAccount request)
    {
        if (!AccessRoles.Todos.Contains(request.Perfil)) return BadRequest();
        // Impede autobloqueio; serializa reduções de privilégios entre administradores.
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString()) return Conflict(new { message = "Use outro administrador para alterar sua conta." });
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73102402)");
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (user.Ativo && await users.IsInRoleAsync(user, AccessRoles.Administrador) &&
            (!request.Ativo || request.Perfil != AccessRoles.Administrador))
        {
            var admins = await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId
                join r in db.Roles on ur.RoleId equals r.Id
                where u.Ativo && r.Name == AccessRoles.Administrador select u.Id).CountAsync();
            if (admins <= 1) return Conflict(new { message = "Mantenha ao menos um administrador ativo." });
        }
        user.NomeCompleto = request.NomeCompleto.Trim(); user.Ativo = request.Ativo;
        if (!(await users.UpdateAsync(user)).Succeeded) return InvalidAccount();
        var roles = await users.GetRolesAsync(user);
        if (!(await users.RemoveFromRolesAsync(user, roles)).Succeeded ||
            !(await users.AddToRoleAsync(user, request.Perfil)).Succeeded ||
            !(await users.UpdateSecurityStampAsync(user)).Succeeded) return InvalidAccount();
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetAccountPassword request)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, request.Password);
        return result.Succeeded ? NoContent() : InvalidAccount();
    }

    private BadRequestObjectResult InvalidAccount() => BadRequest(new { message = "Não foi possível salvar. Confira e-mail único, perfil e senha com 10 caracteres, maiúscula, minúscula, número e símbolo." });
}
