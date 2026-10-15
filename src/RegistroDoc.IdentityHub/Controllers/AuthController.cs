using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RegistroDoc.IdentityHub.Data;
using RegistroDoc.IdentityHub.Security;
using Microsoft.AspNetCore.Authorization;
using RegistroDoc.Contracts.Security;

namespace RegistroDoc.IdentityHub.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    [Authorize]
    [HttpGet("session")]
    public IActionResult Session() => NoContent();

    [Authorize(Roles = AccessRoles.Importacao)]
    [HttpGet("importacao")]
    public IActionResult Importacao() => NoContent();

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        return (await _userManager.UpdateSecurityStampAsync(user)).Succeeded
            ? NoContent() : StatusCode(503);
    }

    [Authorize]
    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(ChangeOwnPassword request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.Password);
        return result.Succeeded ? NoContent() : BadRequest(new { message = "Confira a senha atual e os requisitos da nova senha." });
    }

    private readonly UserManager<IdentityHubUser> _userManager;
    private readonly SignInManager<IdentityHubUser> _signInManager;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(
        UserManager<IdentityHubUser> userManager,
        SignInManager<IdentityHubUser> signInManager,
        JwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "E-mail e senha são obrigatórios."
            });
        }

        var email = request.Email.Trim();

        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return Unauthorized(new
            {
                message = "E-mail ou senha inválidos."
            });
        }

        if (!user.Ativo)
        {
            return Unauthorized(new
            {
                message = "Usuário inativo."
            });
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(new
            {
                message =
                    "Usuário temporariamente bloqueado por excesso de tentativas inválidas."
            });
        }

        var result =
            await _signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return Unauthorized(new
            {
                message =
                    "Usuário temporariamente bloqueado por excesso de tentativas inválidas."
            });
        }

        if (!result.Succeeded)
        {
            return Unauthorized(new
            {
                message = "E-mail ou senha inválidos."
            });
        }

        var token =
            await _jwtTokenService.GenerateAsync(user);

        var roles =
            await _userManager.GetRolesAsync(user);

        return Ok(
            new LoginResponse(
                token.AccessToken,
                token.ExpiresAtUtc,
                user.Id,
                user.Email ?? string.Empty,
                user.NomeCompleto,
                roles.ToArray()));
    }
}

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    Guid UserId,
    string Email,
    string NomeCompleto,
    string[] Roles);
