using System.Net.Http.Headers;

namespace RegistroDoc.Web.Services;

public sealed class ApiTokenHandler : DelegatingHandler
{
    private readonly UserSession _session;

    public ApiTokenHandler(UserSession session)
    {
        _session = session;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_session.IsAuthenticated)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _session.AccessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
