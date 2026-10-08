using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RegistroDoc.Web.Services;

namespace RegistroDoc.UnitTests;

public sealed class ApiTokenHandlerTests
{
    [Fact]
    public async Task DeveUsarTokenDaRequisicaoAtualESemTokenAposLogout()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("registrodoc_access_token", "token-ficticio")], "teste"));
        var recorder = new Recorder();
        using var handler = new ApiTokenHandler(accessor) { InnerHandler = recorder };
        using var client = new HttpClient(handler);
        using var first = await client.GetAsync("http://api/documentos/pdf");
        Assert.Equal("Bearer token-ficticio", recorder.Authorization);
        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        using var second = await client.GetAsync("http://api/documentos/pdf");
        Assert.Null(recorder.Authorization);
    }

    private sealed class Recorder : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
