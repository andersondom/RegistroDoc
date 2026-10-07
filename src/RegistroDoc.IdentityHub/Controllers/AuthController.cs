using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RegistroDoc.IdentityHub.Data;
using RegistroDoc.IdentityHub.Security;

namespace RegistroDoc.IdentityHub.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
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
