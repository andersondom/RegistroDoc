using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RegistroDoc.Api.Controllers;

[ApiController]
[Route("api/security")]
public sealed class SecurityController : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        var email =
            User.FindFirstValue(ClaimTypes.Email);

        var name =
            User.FindFirstValue(ClaimTypes.Name);

        var roles =
            User.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .ToArray();

        return Ok(new
        {
            authenticated = User.Identity?.IsAuthenticated ?? false,
            userId,
            name,
            email,
            roles
        });
    }

    [Authorize(Roles = "Administrador")]
    [HttpGet("admin")]
    public IActionResult Admin()
    {
        return Ok(new
        {
            authorized = true,
            role = "Administrador",
            message = "RegistroDoc.Api reconheceu o Administrador."
        });
    }
}
