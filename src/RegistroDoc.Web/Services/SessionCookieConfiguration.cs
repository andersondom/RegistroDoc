using Microsoft.AspNetCore.Authentication.Cookies;

namespace RegistroDoc.Web.Services;

public static class SessionCookieConfiguration
{
    public static void Configure(CookieAuthenticationOptions options)
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/acesso-negado";
        options.Cookie.Name = "RegistroDoc.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        // O cookie deve expirar junto com o token emitido pelo IdentityHub.
        options.SlidingExpiration = false;
    }
}
