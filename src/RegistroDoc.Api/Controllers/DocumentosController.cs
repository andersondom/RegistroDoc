using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegistroDoc.Contracts.Search;
using RegistroDoc.Infrastructure.Persistence;

namespace RegistroDoc.Api.Controllers;

[ApiController]
[Route("api/documentos")]
[Authorize]
public sealed class DocumentosController : ControllerBase
{
    private readonly RegistroDocDbContext _db;

    public DocumentosController(RegistroDocDbContext db)
    {
        _db = db;
    }

    [HttpGet("pesquisa")]
    [ProducesResponseType<DocumentoSearchResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DocumentoSearchResponse>> Pesquisar(
        [FromQuery] DocumentoSearchRequest request,
        CancellationToken cancellationToken)
    {
        var pagina = Math.Max(1, request.Pagina);
        var tamanhoPagina = Math.Clamp(request.TamanhoPagina, 1, 100);
        var termo = request.Termo?.Trim();

        var documentos = _db.Documentos
            .AsNoTracking()
            .Where(documento => documento.StatusIndexacao == "Concluido");

        if (!string.IsNullOrWhiteSpace(request.TipoDocumento))
        {
            var tipoDocumento = request.TipoDocumento.Trim();
            documentos = documentos.Where(
                documento => documento.TipoDocumento == tipoDocumento);
        }

        if (request.AnoReferencia.HasValue)
        {
            documentos = documentos.Where(
                documento => documento.AnoReferencia == request.AnoReferencia);
        }

        if (!string.IsNullOrWhiteSpace(termo))
        {
            documentos = documentos.Where(documento =>
                _db.PaginasDocumento.Any(paginaDocumento =>
                    paginaDocumento.DocumentoId == documento.Id &&
                    EF.Functions.ILike(
                        paginaDocumento.TextoExtraido,
                        $"%{termo}%")));
        }

        var totalItens = await documentos.CountAsync(cancellationToken);

        var selecionados = await documentos
            .OrderByDescending(documento => documento.IndexadoEmUtc)
            .ThenBy(documento => documento.NomeArquivo)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(documento => new
            {
                documento.Id,
                documento.NomeArquivo,
                documento.TipoDocumento,
                documento.AnoReferencia,
                documento.QuantidadePaginas,
                documento.IndexadoEmUtc
            })
            .ToListAsync(cancellationToken);

        var ids = selecionados.Select(item => item.Id).ToArray();
        var ocorrencias = new Dictionary<Guid, List<(int NumeroPagina, string Texto)>>();

        if (ids.Length > 0 && !string.IsNullOrWhiteSpace(termo))
        {
            var paginasEncontradas = await _db.PaginasDocumento
                .AsNoTracking()
                .Where(paginaDocumento =>
                    ids.Contains(paginaDocumento.DocumentoId) &&
                    EF.Functions.ILike(
                        paginaDocumento.TextoExtraido,
                        $"%{termo}%"))
                .OrderBy(paginaDocumento => paginaDocumento.NumeroPagina)
                .Select(paginaDocumento => new
                {
                    paginaDocumento.DocumentoId,
                    paginaDocumento.NumeroPagina,
                    paginaDocumento.TextoExtraido
                })
                .ToListAsync(cancellationToken);

            ocorrencias = paginasEncontradas
                .GroupBy(item => item.DocumentoId)
                .ToDictionary(
                    grupo => grupo.Key,
                    grupo => grupo
                        .Select(item => (item.NumeroPagina, item.TextoExtraido))
                        .ToList());
        }

        var itens = selecionados
            .Select(documento =>
            {
                ocorrencias.TryGetValue(documento.Id, out var paginasDocumento);
                paginasDocumento ??= [];

                return new DocumentoSearchItem(
                    documento.Id,
                    documento.NomeArquivo,
                    documento.TipoDocumento,
                    documento.AnoReferencia,
                    documento.QuantidadePaginas,
                    documento.IndexadoEmUtc,
                    paginasDocumento
                        .Select(item => item.NumeroPagina)
                        .Distinct()
                        .ToArray(),
                    CriarTrecho(
                        paginasDocumento.FirstOrDefault().Texto,
                        termo));
            })
            .ToArray();

        var totalPaginas = totalItens == 0
            ? 0
            : (int)Math.Ceiling(totalItens / (double)tamanhoPagina);

        return Ok(new DocumentoSearchResponse(
            itens,
            pagina,
            tamanhoPagina,
            totalItens,
            totalPaginas));
    }

    private static string? CriarTrecho(string? texto, string? termo)
    {
        if (string.IsNullOrWhiteSpace(texto) ||
            string.IsNullOrWhiteSpace(termo))
        {
            return null;
        }

        const int raio = 90;
        var indice = texto.IndexOf(
            termo,
            StringComparison.OrdinalIgnoreCase);

        if (indice < 0)
        {
            return texto.Length <= raio * 2
                ? texto
                : texto[..(raio * 2)] + "…";
        }

        var inicio = Math.Max(0, indice - raio);
        var fim = Math.Min(texto.Length, indice + termo.Length + raio);
        var trecho = texto[inicio..fim].Trim();

        if (inicio > 0)
        {
            trecho = "…" + trecho;
        }

        if (fim < texto.Length)
        {
            trecho += "…";
        }

        return trecho;
    }
}
