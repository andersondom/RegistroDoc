namespace RegistroDoc.Api.Services;

public sealed class DocumentAuditMiddleware(RequestDelegate next, ILogger<DocumentAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IAuditService audit)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<AuditarAcessoAttribute>();
        if (metadata is null) { await next(context); return; }

        Guid? documento = Guid.TryParse(context.Request.RouteValues["id"]?.ToString(), out var id) ? id : null;
        Guid registro;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            registro = await audit.IniciarAcessoAsync(metadata.Acao, documento,
                context.Request.Headers.ContainsKey("Range"), timeout.Token);
        }
        catch (Exception)
        {
            // Não registrar exceção, URL, query, cabeçalhos ou dados do documento.
            logger.LogError(new EventId(4101, "AuditoriaInicioFalhou"), "Não foi possível iniciar a auditoria de acesso.");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new { erro = "Serviço temporariamente indisponível." }, CancellationToken.None);
            return;
        }

        int? status = null;
        var resultado = "Interrompido";
        try
        {
            await next(context);
            status = context.Response.StatusCode;
            resultado = context.RequestAborted.IsCancellationRequested ? "Cancelado" : status switch
            {
                401 or 403 => "Negado",
                >= 400 => "ErroHttp",
                _ => "RespostaConcluida"
            };
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            resultado = "Cancelado";
            throw;
        }
        catch (Exception)
        {
            resultado = "ErroExecucao";
            // Depois de enviados os cabeçalhos, não declarar um status diferente do enviado.
            status = context.Response.HasStarted ? context.Response.StatusCode : 500;
            throw;
        }
        finally
        {
            try
            {
                // O cancelamento do cliente não deve apagar a tentativa já registrada.
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await audit.ConcluirAcessoAsync(registro, status, resultado, timeout.Token);
            }
            catch (Exception)
            {
                // A resposta pode já ter sido enviada. Mantém Iniciado para conciliação.
                logger.LogError(new EventId(4102, "AuditoriaConclusaoFalhou"),
                    "Não foi possível concluir a auditoria {AuditoriaId}.", registro);
            }
        }
    }
}
