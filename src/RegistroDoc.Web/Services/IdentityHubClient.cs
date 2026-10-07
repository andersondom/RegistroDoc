using System.Net;
using System.Net.Http.Json;

namespace RegistroDoc.Web.Services;

public sealed class IdentityHubClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public IdentityHubClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<LoginResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("RegistroDoc.IdentityHub");
        using var response = await client.PostAsJsonAsync(
            "api/auth/login",
            new LoginRequest(email, password),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return LoginResult.Failure("E-mail ou senha inválidos.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return LoginResult.Failure(
                "Não foi possível autenticar no momento.");
        }

        var login = await response.Content
            .ReadFromJsonAsync<LoginResponse>(
                cancellationToken: cancellationToken);

        return login is null
            ? LoginResult.Failure("Resposta de autenticação inválida.")
            : LoginResult.Success(login);
    }

    public sealed record LoginRequest(string Email, string Password);

    public sealed record LoginResponse(
        string AccessToken,
        DateTime ExpiresAtUtc,
        Guid UserId,
        string Email,
        string NomeCompleto,
        string[] Roles);

    public sealed record LoginResult(
        bool Succeeded,
        LoginResponse? Response,
        string? Error)
    {
        public static LoginResult Success(LoginResponse response) =>
            new(true, response, null);

        public static LoginResult Failure(string error) =>
            new(false, null, error);
    }
}
