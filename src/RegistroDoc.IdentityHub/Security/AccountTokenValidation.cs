using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using RegistroDoc.IdentityHub.Data;

namespace RegistroDoc.IdentityHub.Security;

public static class AccountTokenValidation
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<IdentityHubUser>>();
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = context.Principal?.FindFirstValue("security_stamp");
        var user = Guid.TryParse(id, out _) ? await users.FindByIdAsync(id!) : null;
        if (user is null || !user.Ativo || string.IsNullOrEmpty(stamp) ||
            stamp != await users.GetSecurityStampAsync(user))
        {
            context.Fail("Sessão inválida.");
            return;
        }
        var roles = await users.GetRolesAsync(user);
        var tokenRoles = context.Principal!.FindAll(ClaimTypes.Role).Select(c => c.Value);
        if (!roles.Order().SequenceEqual(tokenRoles.Order())) context.Fail("Sessão inválida.");
    }
}
