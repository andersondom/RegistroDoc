using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.Authorization;

namespace RegistroDoc.Web.Services;

public sealed class ApiTokenHandler : DelegatingHandler
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public ApiTokenHandler(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var authenticationState =
            await _authenticationStateProvider.GetAuthenticationStateAsync();

        var accessToken =
            authenticationState.User.FindFirst("registrodoc_access_token")?.Value;

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
