using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace RegistroDoc.IntegrationTests;

public sealed class DocumentHttpAuthorizationTests : IClassFixture<DocumentHttpAuthorizationTests.ApiFactory>
{
    private readonly HttpClient _client;
    private const string SigningKey = "registrodoc-test-key-not-for-production-1234567890";

    public DocumentHttpAuthorizationTests(ApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Theory]
    [InlineData("/api/documentos/pesquisa")]
    [InlineData("/api/documentos/11111111-1111-4111-8111-111111111111/arquivo")]
    public async Task AnonymousRequestReturnsUnauthorized(string path)
    {
        using var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/documentos/pesquisa")]
    [InlineData("/api/documentos/11111111-1111-4111-8111-111111111111/arquivo")]
    public async Task AuthenticatedUserWithoutDocumentRoleReturnsForbidden(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("Visitante"));
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/documentos/pesquisa")]
    [InlineData("/api/documentos/11111111-1111-4111-8111-111111111111/arquivo")]
    public async Task InvalidTokenReturnsUnauthorized(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string CreateToken(string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var token = new JwtSecurityToken(
            issuer: "registrodoc-http-test",
            audience: "registrodoc-http-test-client",
            claims: new[] { new Claim(ClaimTypes.Role, role) },
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public sealed class ApiFactory : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            // UseSetting is available before Program reads builder.Configuration.
            builder.UseSetting("ConnectionStrings:RegistroDoc", "Host=localhost;Database=registrodoc_test;Username=test;Password=test");
            builder.UseSetting("Jwt:Issuer", "registrodoc-http-test");
            builder.UseSetting("Jwt:Audience", "registrodoc-http-test-client");
            builder.UseSetting("Jwt:SigningKey", SigningKey);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:RegistroDoc"] = "Host=localhost;Database=registrodoc_test;Username=test;Password=test",
                    ["Jwt:Issuer"] = "registrodoc-http-test",
                    ["Jwt:Audience"] = "registrodoc-http-test-client",
                    ["Jwt:SigningKey"] = SigningKey
                });
            });
        }
    }
}
