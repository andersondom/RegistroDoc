using System.Net.Http.Headers;

namespace RegistroDoc.Web.Services;

public sealed class ApiTokenHandler : DelegatingHandler
{
    private readonly IConfiguration _configuration;

    public ApiTokenHandler(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = _configuration["Api:AccessToken"];

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
