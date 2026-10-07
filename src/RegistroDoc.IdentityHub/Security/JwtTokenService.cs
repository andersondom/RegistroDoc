using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using RegistroDoc.IdentityHub.Data;

namespace RegistroDoc.IdentityHub.Security;

public sealed class JwtTokenService
{
    private readonly UserManager<IdentityHubUser> _userManager;
    private readonly JwtOptions _options;

    public JwtTokenService(
        UserManager<IdentityHubUser> userManager,
        IConfiguration configuration)
    {
        _userManager = userManager;

        _options = new JwtOptions();

        configuration
            .GetSection(JwtOptions.SectionName)
            .Bind(_options);

        if (string.IsNullOrWhiteSpace(_options.Issuer))
        {
            throw new InvalidOperationException(
                "Jwt:Issuer não foi configurado.");
        }

        if (string.IsNullOrWhiteSpace(_options.Audience))
        {
            throw new InvalidOperationException(
                "Jwt:Audience não foi configurado.");
        }

        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey não foi configurado.");
        }

        if (_options.ExpirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                "Jwt:ExpirationMinutes deve ser maior que zero.");
        }
    }

    public async Task<JwtTokenResult> GenerateAsync(
        IdentityHubUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var now = DateTime.UtcNow;

        var expiresAt =
            now.AddMinutes(_options.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString()),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString()),

            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new(
                ClaimTypes.Name,
                user.UserName ?? string.Empty),

            new(
                ClaimTypes.Email,
                user.Email ?? string.Empty)
        };

        foreach (var role in roles)
        {
            claims.Add(
                new Claim(
                    ClaimTypes.Role,
                    role));
        }

        var signingKey =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _options.SigningKey));

        var credentials =
            new SigningCredentials(
                signingKey,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: now,
                expires: expiresAt,
                signingCredentials: credentials);

        var tokenValue =
            new JwtSecurityTokenHandler()
                .WriteToken(token);

        return new JwtTokenResult(
            tokenValue,
            expiresAt);
    }
}

public sealed record JwtTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc);
