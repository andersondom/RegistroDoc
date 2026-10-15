using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RegistroDoc.Api.Controllers;
using RegistroDoc.Contracts.Security;
using RegistroDoc.IdentityHub.Controllers;
using RegistroDoc.IdentityHub.Data;
using RegistroDoc.Indexer.Importing;
using RegistroDoc.Infrastructure.Persistence;
using RegistroDoc.Web.Services;

namespace RegistroDoc.IntegrationTests;

[Collection("Auditoria HTTP")]
public sealed class AccountAccessIntegrationTests
{
    [Fact]
    public async Task ContasPerfisRevogacaoECookie_DevemProtegerOperacoesReais()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RegistroDoc");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        void Configure(IWebHostBuilder b) => b.UseEnvironment("Production")
            .UseSetting("ConnectionStrings:RegistroDoc", connection)
            .UseSetting("ConnectionStrings:IdentityHub", connection)
            .UseSetting("Jwt:Issuer", "accounts-test").UseSetting("Jwt:Audience", "accounts-test")
            .UseSetting("Jwt:SigningKey", key).UseSetting("Jwt:ExpirationMinutes", "30");
        await using var identity = new WebApplicationFactory<UsersController>().WithWebHostBuilder(Configure);
        using var identityClient = identity.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await using var api = new WebApplicationFactory<DocumentosController>().WithWebHostBuilder(b =>
        {
            Configure(b);
            b.ConfigureServices(s => s.AddHttpClient("IdentitySession", c => c.BaseAddress = new Uri("https://localhost/"))
                .ConfigurePrimaryHttpMessageHandler(() => identity.Server.CreateHandler()));
        });
        using var apiClient = api.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await using var web = new WebApplicationFactory<IdentityHubClient>().WithWebHostBuilder(b =>
        {
            Configure(b);
            b.ConfigureServices(s =>
            {
                foreach (var name in new[] { "RegistroDoc.IdentityHub", "RegistroDoc.IdentityAdmin" })
                    s.AddHttpClient(name, c => c.BaseAddress = new Uri("https://localhost/"))
                        .ConfigurePrimaryHttpMessageHandler(() => identity.Server.CreateHandler());
                s.AddHttpClient("RegistroDoc.Api", c => c.BaseAddress = new Uri("https://localhost/"))
                    .ConfigurePrimaryHttpMessageHandler(() => api.Server.CreateHandler());
            });
        });
        var suffix = Guid.NewGuid().ToString("N");
        var password = "Ficticio!" + suffix;
        var adminEmail = $"admin-{suffix}@example.invalid";
        Guid adminId;
        await using (var scope = identity.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityHubUser>>();
            var admin = new IdentityHubUser { UserName = adminEmail, Email = adminEmail, NomeCompleto = "Administrador fictício" };
            Assert.True((await users.CreateAsync(admin, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, AccessRoles.Administrador)).Succeeded);
            adminId = admin.Id;
            var noRole = new IdentityHubUser { UserName = $"sem-perfil-{suffix}@example.invalid", Email = $"sem-perfil-{suffix}@example.invalid", NomeCompleto = "Sem perfil" };
            Assert.True((await users.CreateAsync(noRole, password)).Succeeded);
        }
        async Task<string> Login(string email, string? pass = null)
        {
            using var response = await identityClient.PostAsJsonAsync("/api/auth/login", new { email, password = pass ?? password });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        }
        async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string? token = null, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request);
        }
        async Task Expect(HttpClient client, HttpMethod method, string path, string? token, HttpStatusCode status, object? body = null)
        { using var response = await Send(client, method, path, token, body); Assert.Equal(status, response.StatusCode); }
        var adminToken = await Login(adminEmail);
        var accounts = new Dictionary<string, (Guid Id, string Email, string Token)>();
        foreach (var role in AccessRoles.Todos)
        {
            var email = $"{role}-{suffix}@example.invalid";
            using var created = await Send(identityClient, HttpMethod.Post, "/api/users", adminToken,
                new CreateAccount(email, "Pessoa fictícia", password, role));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<CreatedAccount>())!.Id;
            accounts[role] = (id, email, await Login(email));
        }
        try
        {
            var noRoleToken = await Login($"sem-perfil-{suffix}@example.invalid");
            await Expect(apiClient, HttpMethod.Get, "/api/documentos/pesquisa", noRoleToken, HttpStatusCode.Forbidden);
            await Expect(apiClient, HttpMethod.Get, $"/api/documentos/{Guid.NewGuid()}/arquivo", noRoleToken, HttpStatusCode.Forbidden);
            await Expect(identityClient, HttpMethod.Get, "/api/users", null, HttpStatusCode.Unauthorized);
            foreach (var role in AccessRoles.Todos)
            {
                var token = accounts[role].Token;
                await Expect(apiClient, HttpMethod.Get, "/api/documentos/pesquisa", token, HttpStatusCode.OK);
                await Expect(apiClient, HttpMethod.Get, $"/api/documentos/{Guid.NewGuid()}/arquivo", token, HttpStatusCode.NotFound);
                await Expect(identityClient, HttpMethod.Get, "/api/users", token,
                    role == AccessRoles.Administrador ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
                await Expect(apiClient, HttpMethod.Get, "/api/auditorias", token,
                    role == AccessRoles.Administrador ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
                await Expect(identityClient, HttpMethod.Get, "/api/auth/importacao", token,
                    role == AccessRoles.Consulta ? HttpStatusCode.Forbidden : HttpStatusCode.NoContent);
                if (role != AccessRoles.Administrador)
                {
                    await Expect(identityClient, HttpMethod.Post, "/api/users", token, HttpStatusCode.Forbidden,
                        new CreateAccount("blocked@example.invalid", "Fictício", password, AccessRoles.Administrador));
                    await Expect(identityClient, HttpMethod.Put, $"/api/users/{adminId}", token, HttpStatusCode.Forbidden,
                        new UpdateAccount("Fictício", false, AccessRoles.Consulta));
                    await Expect(identityClient, HttpMethod.Post, $"/api/users/{adminId}/password", token, HttpStatusCode.Forbidden,
                        new ResetAccountPassword(password));
                }
            }
            await Expect(identityClient, HttpMethod.Post, "/api/users", adminToken, HttpStatusCode.BadRequest,
                new CreateAccount(adminEmail, "Duplicado", password, AccessRoles.Consulta));
            await Expect(identityClient, HttpMethod.Post, "/api/users", adminToken, HttpStatusCode.BadRequest,
                new CreateAccount("invalid@example.invalid", "Fictício", password, "SuperAdmin"));
            await Expect(identityClient, HttpMethod.Post, "/api/users", adminToken, HttpStatusCode.BadRequest,
                new CreateAccount("weak@example.invalid", "Fictício", "abcdefghij", AccessRoles.Consulta));
            await Expect(identityClient, HttpMethod.Put, $"/api/users/{adminId}", adminToken, HttpStatusCode.Conflict,
                new UpdateAccount("Fictício", false, AccessRoles.Consulta));

            // O importador usa a mesma autorização do servidor, inclusive quando a sessão é revogada.
            using var importer = identity.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ImportAuthorization.LoginAsync(importer,
                accounts[AccessRoles.Consulta].Email, password, default));
            await ImportAuthorization.LoginAsync(importer, accounts[AccessRoles.Operador].Email, password, default);

            using var browser = web.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            async Task<string> Csrf(string path)
            {
                var html = await browser.GetStringAsync(path);
                var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
                Assert.True(match.Success);
                return WebUtility.HtmlDecode(match.Groups[1].Value);
            }
            using (var response = await browser.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
                { ["__RequestVerificationToken"] = await Csrf("/login"), ["email"] = adminEmail, ["password"] = password })))
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("Contas individuais", await browser.GetStringAsync("/contas"));
            Assert.Contains("Auditoria de acesso", await browser.GetStringAsync("/auditorias"));
            using (var response = await browser.PostAsync("/contas/criar", new FormUrlEncodedContent(new Dictionary<string, string>())))
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var form = new Dictionary<string, string> { ["__RequestVerificationToken"] = await Csrf("/contas"),
                ["nome"] = "Conta via formulário", ["email"] = $"form-{suffix}@example.invalid", ["senha"] = password, ["perfil"] = AccessRoles.Consulta };
            using (var response = await browser.PostAsync("/contas/criar", new FormUrlEncodedContent(form)))
            { Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.Contains("resultado=ok", response.Headers.Location!.ToString()); }

            var reader = accounts[AccessRoles.Consulta];
            using var readerBrowser = web.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            var readerHtml = await readerBrowser.GetStringAsync("/login");
            var readerCsrf = WebUtility.HtmlDecode(Regex.Match(readerHtml, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            using (var login = await readerBrowser.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
                { ["__RequestVerificationToken"] = readerCsrf, ["email"] = reader.Email, ["password"] = password })))
                Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            using (var forbidden = await readerBrowser.GetAsync("/contas"))
            { Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode); Assert.Contains("acesso-negado", forbidden.Headers.Location!.ToString()); }
            using (var forbidden = await readerBrowser.PostAsync("/contas/criar", new FormUrlEncodedContent(form)))
                Assert.True(forbidden.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Forbidden);
            await Expect(identityClient, HttpMethod.Put, $"/api/users/{reader.Id}", adminToken, HttpStatusCode.NoContent,
                new UpdateAccount("Fictício", false, AccessRoles.Consulta));
            await Expect(apiClient, HttpMethod.Get, "/api/documentos/pesquisa", reader.Token, HttpStatusCode.Unauthorized);
            using (var staleCookie = await readerBrowser.GetAsync("/pesquisa"))
            { Assert.Equal(HttpStatusCode.Redirect, staleCookie.StatusCode); Assert.Contains("login", staleCookie.Headers.Location!.ToString()); }
            await Expect(identityClient, HttpMethod.Post, "/api/auth/login", null, HttpStatusCode.Unauthorized,
                new { email = reader.Email, password });
            await Expect(identityClient, HttpMethod.Put, $"/api/users/{reader.Id}", adminToken, HttpStatusCode.NoContent,
                new UpdateAccount("Fictício", true, AccessRoles.Consulta));
            await Expect(apiClient, HttpMethod.Get, "/api/documentos/pesquisa", reader.Token, HttpStatusCode.Unauthorized);
            var readerToken = await Login(reader.Email);
            await Expect(identityClient, HttpMethod.Post, $"/api/users/{reader.Id}/password", adminToken, HttpStatusCode.NoContent,
                new ResetAccountPassword(password + "A"));
            await Expect(apiClient, HttpMethod.Get, "/api/documentos/pesquisa", readerToken, HttpStatusCode.Unauthorized);
            readerToken = await Login(reader.Email, password + "A");
            await Expect(identityClient, HttpMethod.Post, "/api/auth/password", readerToken, HttpStatusCode.NoContent,
                new ChangeOwnPassword(password + "A", password + "B"));
            await Expect(apiClient, HttpMethod.Get, "/api/documentos/pesquisa", readerToken, HttpStatusCode.Unauthorized);

            var op = accounts[AccessRoles.Operador];
            await Expect(identityClient, HttpMethod.Put, $"/api/users/{op.Id}", adminToken, HttpStatusCode.NoContent,
                new UpdateAccount("Fictício", true, AccessRoles.Consulta));
            await Expect(apiClient, HttpMethod.Get, "/api/documentos/pesquisa", op.Token, HttpStatusCode.Unauthorized);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ImportAuthorization.CheckAsync(importer, default));
            await Expect(identityClient, HttpMethod.Get, "/api/auth/importacao", await Login(op.Email), HttpStatusCode.Forbidden);

            using (var logout = await browser.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string>
                { ["__RequestVerificationToken"] = await Csrf("/conta") }))) Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
            await Expect(apiClient, HttpMethod.Get, "/api/documentos/pesquisa", adminToken, HttpStatusCode.Unauthorized);
            using (var denied = await browser.GetAsync($"/documentos/{Guid.NewGuid()}/pdf"))
                Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);

            await using var auditScope = api.Services.CreateAsyncScope();
            var db = auditScope.ServiceProvider.GetRequiredService<RegistroDocDbContext>();
            Assert.True(await db.Auditorias.AnyAsync(a => a.UsuarioId == reader.Id.ToString() && a.Acao == "PesquisarDocumentos"));
        }
        finally
        {
            await using var scope = identity.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityHubDbContext>();
            await db.Users.Where(u => u.Email!.Contains(suffix)).ExecuteDeleteAsync();
        }
    }

    private sealed record CreatedAccount(Guid Id);
}
