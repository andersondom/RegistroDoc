using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroDoc.IdentityHub.Security;

namespace RegistroDoc.IdentityHub.Controllers;

[ApiController]
[Route("api/security-test")]
public sealed class SecurityTestController : ControllerBase
{
    [Authorize]
    [HttpGet("authenticated")]
    public IActionResult Authenticated()
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        var email =
            User.FindFirstValue(ClaimTypes.Email);

        var roles =
            User.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .ToArray();

        return Ok(new
        {
            authenticated = true,
            userId,
            email,
            roles
        });
    }

    [Authorize(Roles = IdentityRoles.Administrador)]
    [HttpGet("administrator")]
    public IActionResult Administrator()
    {
        return Ok(new
        {
            authorized = true,
            role = IdentityRoles.Administrador,
            message = "Acesso administrativo autorizado."
        });
    }
}
