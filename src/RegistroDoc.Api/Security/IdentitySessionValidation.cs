using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace RegistroDoc.Api.Security;

public static class IdentitySessionValidation
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var factory = context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/session");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(context.Request.Headers.Authorization.ToString());
        try
        {
            using var response = await factory.CreateClient("IdentitySession").SendAsync(request, context.HttpContext.RequestAborted);
            if (response.StatusCode != System.Net.HttpStatusCode.NoContent) context.Fail("Sessão inválida.");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            context.Fail("Não foi possível validar a sessão.");
        }
    }
}
