using System.Net;
using System.Reflection;
using RegistroDoc.Web.Components.Pages;

namespace RegistroDoc.UnitTests;

public sealed class PesquisaRegressionTests
{
    [Fact]
    public async Task NavegacaoENovaPesquisa_DevemUsarFiltrosAtuais()
    {
        var factory = new RecordingFactory();
        var page = new TestPesquisa();
        typeof(Pesquisa).GetProperty("HttpClientFactory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, factory);
        page.SetPage(2);
        page.Termo = "JASMIM & AZUL";
        page.AnoReferencia = 2026;
        await page.LoadAsync();
        Assert.Contains("Pagina=2", factory.LastUrl);
        Assert.Contains("Termo=JASMIM%20%26%20AZUL", factory.LastUrl);
        Assert.Equal("/pesquisa/3?Termo=JASMIM%20%26%20AZUL&AnoReferencia=2026", PageUrl(page, 3));

        page.SetPage(1);
        page.Termo = "GIRASSOL DOURADO";
        page.AnoReferencia = 2025;
        await page.LoadAsync();
        Assert.Contains("Pagina=1", factory.LastUrl);
        Assert.Contains("AnoReferencia=2025", factory.LastUrl);
        Assert.DoesNotContain("JASMIM", factory.LastUrl);

        page.Termo = null;
        page.AnoReferencia = null;
        await page.LoadAsync();
        Assert.DoesNotContain("Termo=", factory.LastUrl);
        Assert.DoesNotContain("AnoReferencia=", factory.LastUrl);
        Assert.Equal("/pesquisa/1", PageUrl(page, 0));

        page.TipoDocumento = "Categoria Ficticia";
        await page.LoadAsync();
        Assert.Contains("TipoDocumento=Categoria%20Ficticia", factory.LastUrl);
        Assert.Equal("/pesquisa/2?TipoDocumento=Categoria%20Ficticia", PageUrl(page, 2));
        page.TipoDocumento = "";
        await page.LoadAsync();
        Assert.DoesNotContain("TipoDocumento=", factory.LastUrl);
    }

    private static string PageUrl(Pesquisa page, int number) =>
        (string)typeof(Pesquisa).GetMethod("ObterUrlPagina", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(page, [number])!;

    private sealed class TestPesquisa : Pesquisa
    {
        public void SetPage(int page) => Pagina = page;
        public Task LoadAsync() => OnParametersSetAsync();
    }

    private sealed class RecordingFactory : HttpMessageHandler, IHttpClientFactory
    {
        public string LastUrl { get; private set; } = "";
        public HttpClient CreateClient(string name) => new(this) { BaseAddress = new Uri("http://api/") };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri!.OriginalString;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri.AbsolutePath.EndsWith("/tipos") ? "[]" : "{\"itens\":[],\"pagina\":1,\"tamanhoPagina\":20,\"totalItens\":0,\"totalPaginas\":0}",
                    System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
