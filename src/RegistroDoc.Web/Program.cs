using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using RegistroDoc.Web.Components;
using RegistroDoc.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("RegistroDoc.Web");
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(SessionCookieConfiguration.Configure);

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<ApiTokenHandler>();
builder.Services.AddScoped<IdentityHubClient>();

var apiBaseUrl =
    builder.Configuration["Api:BaseUrl"]
    ?? "http://localhost:8082/";

var identityHubBaseUrl =
    builder.Configuration["IdentityHub:BaseUrl"]
    ?? "http://localhost:8081/";

builder.Services.AddHttpClient(
        "RegistroDoc.Api",
        client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<ApiTokenHandler>();

builder.Services.AddHttpClient(
    "RegistroDoc.IdentityHub",
    client => client.BaseAddress = new Uri(identityHubBaseUrl));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute(
    "/not-found",
    createScopeForStatusCodePages: true);

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapPost("/auth/login", async (
    HttpContext httpContext,
    IAntiforgery antiforgery,
    IdentityHubClient identityHub,
    CancellationToken cancellationToken) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(httpContext);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
    var email = form["email"].ToString();
    var password = form["password"].ToString();

    if (string.IsNullOrWhiteSpace(email) ||
        string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect("/login?erro=campos");
    }

    var result = await identityHub.LoginAsync(
        email,
        password,
        cancellationToken);

    if (!result.Succeeded || result.Response is null)
    {
        return Results.Redirect("/login?erro=credenciais");
    }

    var login = result.Response;
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, login.UserId.ToString()),
        new(ClaimTypes.Name, login.NomeCompleto),
        new(ClaimTypes.Email, login.Email),
        new("registrodoc_access_token", login.AccessToken)
    };

    claims.AddRange(
        login.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

    var identity = new ClaimsIdentity(
        claims,
        CookieAuthenticationDefaults.AuthenticationScheme);

    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = new DateTimeOffset(login.ExpiresAtUtc)
        });

    return Results.Redirect("/pesquisa");
});

app.MapPost("/auth/logout", async (HttpContext httpContext, IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(httpContext);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    await httpContext.SignOutAsync(
        CookieAuthenticationDefaults.AuthenticationScheme);

    return Results.Redirect("/login");
}).RequireAuthorization();

app.MapGet("/documentos/{id:guid}/pdf", async (
    Guid id,
    HttpContext httpContext,
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken) =>
{
    httpContext.Response.Headers.CacheControl = "no-store, private";
    var client = httpClientFactory.CreateClient("RegistroDoc.Api");
    using var response = await client.GetAsync(
        $"api/documentos/{id}/arquivo",
        HttpCompletionOption.ResponseHeadersRead,
        cancellationToken);

    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return Results.NotFound();
    }

    if (!response.IsSuccessStatusCode)
    {
        return Results.StatusCode((int)response.StatusCode);
    }

    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
    return Results.File(bytes, "application/pdf", enableRangeProcessing: true);
}).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
