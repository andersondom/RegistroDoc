using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace RegistroDoc.Web.Services;

public static class AccountCookieValidation
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var token = context.Principal?.FindFirst("registrodoc_access_token")?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                var factory = context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                using var client = factory.CreateClient("RegistroDoc.IdentityHub");
                using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/session");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await client.SendAsync(request, context.HttpContext.RequestAborted);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { }
        }
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync();
    }
}
