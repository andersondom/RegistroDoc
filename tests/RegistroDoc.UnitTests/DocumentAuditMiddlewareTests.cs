using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using RegistroDoc.Api.Services;

namespace RegistroDoc.UnitTests;

public sealed class DocumentAuditMiddlewareTests
{
    private static DefaultHttpContext Context(bool audited = true)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        if (audited) context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuditarAcessoAttribute("LerPdf")), "teste"));
        context.Request.QueryString = new QueryString("?Termo=SEGREDO_FICTICIO");
        return context;
    }

    [Fact]
    public async Task FalhaInicial_DeveBloquearConteudoCom503()
    {
        var context = Context();
        var audit = new FakeAudit { FailStart = true };
        var called = false;
        var middleware = new DocumentAuditMiddleware(_ => { called = true; return Task.CompletedTask; },
            NullLogger<DocumentAuditMiddleware>.Instance);
        await middleware.InvokeAsync(context, audit);
        Assert.False(called);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("SEGREDO", body);
        Assert.DoesNotContain("%PDF", body);
        Assert.Null(audit.Outcome);
    }

    [Fact]
    public async Task FalhaFinal_DevePreservarTentativaERespostaJaProduzida()
    {
        var context = Context();
        var audit = new FakeAudit { FailFinish = true };
        await new DocumentAuditMiddleware(c => c.Response.WriteAsync("%PDF-FICTICIO"),
            NullLogger<DocumentAuditMiddleware>.Instance).InvokeAsync(context, audit);
        Assert.Equal(1, audit.Starts);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("RespostaConcluida", audit.Outcome);
    }

    [Fact]
    public async Task Cancelamento_DeveConcluirComTokenIndependenteDoCliente()
    {
        var context = Context();
        using var cancelled = new CancellationTokenSource();
        context.RequestAborted = cancelled.Token;
        var audit = new FakeAudit();
        var middleware = new DocumentAuditMiddleware(_ => { cancelled.Cancel(); throw new OperationCanceledException(cancelled.Token); },
            NullLogger<DocumentAuditMiddleware>.Instance);
        await Assert.ThrowsAsync<OperationCanceledException>(() => middleware.InvokeAsync(context, audit));
        Assert.Equal("Cancelado", audit.Outcome);
        Assert.Null(audit.Status);
        Assert.False(audit.FinishCancelled);
    }

    [Fact]
    public async Task ErroDoEndpoint_DeveRegistrarSemMascararExcecao()
    {
        var context = Context();
        var audit = new FakeAudit();
        var middleware = new DocumentAuditMiddleware(_ => throw new InvalidOperationException("SEGREDO_FICTICIO"),
            NullLogger<DocumentAuditMiddleware>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, audit));
        Assert.Equal("ErroExecucao", audit.Outcome);
        Assert.Equal(500, audit.Status);
    }

    [Fact]
    public async Task RotaSemMetadado_NaoDeveGerarAuditoria()
    {
        var audit = new FakeAudit();
        await new DocumentAuditMiddleware(_ => Task.CompletedTask, NullLogger<DocumentAuditMiddleware>.Instance)
            .InvokeAsync(Context(false), audit);
        Assert.Equal(0, audit.Starts);
    }

    private sealed class FakeAudit : IAuditService
    {
        public bool FailStart { get; init; }
        public bool FailFinish { get; init; }
        public int Starts { get; private set; }
        public string? Outcome { get; private set; }
        public int? Status { get; private set; }
        public bool FinishCancelled { get; private set; }
        public Task<Guid> IniciarAcessoAsync(string action, Guid? documentId, bool range, CancellationToken cancellationToken = default)
        {
            Starts++;
            if (FailStart) throw new IOException("SEGREDO_FICTICIO");
            return Task.FromResult(Guid.NewGuid());
        }
        public Task ConcluirAcessoAsync(Guid id, int? status, string outcome, CancellationToken cancellationToken = default)
        {
            Outcome = outcome; Status = status; FinishCancelled = cancellationToken.IsCancellationRequested;
            if (FailFinish) throw new IOException("SEGREDO_FICTICIO");
            return Task.CompletedTask;
        }
        public Task RegistrarAsync(string entidade, string entidadeId, string acao, string? descricao = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
