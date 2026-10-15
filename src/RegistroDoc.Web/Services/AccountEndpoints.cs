using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using RegistroDoc.Contracts.Security;

namespace RegistroDoc.Web.Services;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapPost("/contas/{operation}", HandleAsync).RequireAuthorization(p => p.RequireRole(AccessRoles.Administrador));
        app.MapPost("/conta/senha", ChangePasswordAsync).RequireAuthorization();
    }

    private static async Task<IResult> HandleAsync(string operation, HttpContext context,
        IAntiforgery antiforgery, IHttpClientFactory clients)
    {
        try { await antiforgery.ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { return Results.BadRequest(); }
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        using var client = clients.CreateClient("RegistroDoc.IdentityAdmin");
        HttpResponseMessage response;
        try
        {
            if (operation == "criar") response = await client.PostAsJsonAsync("api/users",
                new CreateAccount(form["email"].ToString(), form["nome"].ToString(), form["senha"].ToString(), form["perfil"].ToString()), context.RequestAborted);
            else if (Guid.TryParse(form["id"], out var id) && operation == "salvar")
                response = await client.PutAsJsonAsync($"api/users/{id}",
                    new UpdateAccount(form["nome"].ToString(), form["ativo"] == "true", form["perfil"].ToString()), context.RequestAborted);
            else if (Guid.TryParse(form["id"], out id) && operation == "senha")
                response = await client.PostAsJsonAsync($"api/users/{id}/password", new ResetAccountPassword(form["senha"].ToString()), context.RequestAborted);
            else return Results.BadRequest();
            using (response) return Results.Redirect(response.IsSuccessStatusCode ? "/contas?resultado=ok" : "/contas?resultado=erro");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { return Results.Redirect("/contas?resultado=indisponivel"); }
    }

    private static async Task<IResult> ChangePasswordAsync(HttpContext context, IAntiforgery antiforgery, IHttpClientFactory clients)
    {
        try { await antiforgery.ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { return Results.BadRequest(); }
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        try
        {
            using var client = clients.CreateClient("RegistroDoc.IdentityAdmin");
            using var response = await client.PostAsJsonAsync("api/auth/password",
                new ChangeOwnPassword(form["atual"].ToString(), form["senha"].ToString()), context.RequestAborted);
            if (!response.IsSuccessStatusCode) return Results.Redirect("/conta?erro=senha");
            await context.SignOutAsync();
            return Results.Redirect("/login");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { return Results.Redirect("/conta?erro=indisponivel"); }
    }
}
