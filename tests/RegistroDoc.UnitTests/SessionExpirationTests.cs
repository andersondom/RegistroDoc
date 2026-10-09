using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using RegistroDoc.Web.Services;

namespace RegistroDoc.UnitTests;

public class SessionExpirationTests
{
    [Fact]
    public async Task CookieDeveExpirarNoPrazoDoTokenMesmoComAcessoDuranteASessao()
    {
        var clock = new SessionClock();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                SessionCookieConfiguration.Configure(options);
                options.TimeProvider = clock;
            });
        services.AddTransient<CookieAuthenticationHandler, CompletableCookieHandler>();
        using var provider = services.BuildServiceProvider();

        DefaultHttpContext Context(IServiceProvider scopedProvider, string? cookie = null)
        {
            var context = new DefaultHttpContext { RequestServices = scopedProvider };
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("localhost");
            if (cookie is not null) context.Request.Headers.Cookie = cookie;
            return context;
        }

        string cookie;
        var expires = clock.GetUtcNow().AddMinutes(10);
        using (var scope = provider.CreateScope())
        {
            var context = Context(scope.ServiceProvider);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "usuario-ficticio") },
                CookieAuthenticationDefaults.AuthenticationScheme));
            await context.SignInAsync(principal, new AuthenticationProperties { ExpiresUtc = expires });
            cookie = context.Response.Headers.SetCookie.ToString().Split(';')[0];
        }

        // Acesso após metade da validade: o padrão do middleware renovaria o cookie.
        clock.Advance(TimeSpan.FromMinutes(6));
        using (var scope = provider.CreateScope())
        {
            var context = Context(scope.ServiceProvider, cookie);
            var result = await context.AuthenticateAsync();
            Assert.True(result.Succeeded);
            Assert.Equal(expires, result.Properties!.ExpiresUtc);
            var handler = await scope.ServiceProvider.GetRequiredService<IAuthenticationHandlerProvider>()
                .GetHandlerAsync(context, CookieAuthenticationDefaults.AuthenticationScheme);
            // FinishResponseAsync processa a renovação ao finalizar a resposta.
            await ((CompletableCookieHandler)handler!).CompleteAsync();
            Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        }

        clock.Advance(TimeSpan.FromMinutes(5));
        using (var scope = provider.CreateScope())
        {
            var context = Context(scope.ServiceProvider, cookie);
            var result = await context.AuthenticateAsync();
            Assert.False(result.Succeeded);
            await context.ChallengeAsync();
            Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
            Assert.Contains("/login", context.Response.Headers.Location.ToString());
        }
    }

    private sealed class CompletableCookieHandler(
        IOptionsMonitor<CookieAuthenticationOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : CookieAuthenticationHandler(options, logger, encoder)
    {
        public Task CompleteAsync() => FinishResponseAsync();
    }

    private sealed class SessionClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }
}
