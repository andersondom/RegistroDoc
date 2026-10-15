using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace RegistroDoc.Indexer.Importing;

public static class ImportAuthorization
{
    public static async Task LoginAsync(HttpClient client, string email, string password, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("api/auth/login", new { email, password }, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new UnauthorizedAccessException();
        var login = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(login?.AccessToken)) throw new UnauthorizedAccessException();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        await CheckAsync(client, cancellationToken);
    }

    public static async Task CheckAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("api/auth/importacao", cancellationToken);
        if (response.StatusCode != System.Net.HttpStatusCode.NoContent) throw new UnauthorizedAccessException();
    }

    public sealed record Credentials(string Email, string Password);
    private sealed record TokenResponse(string AccessToken);
}
