using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using RegistroDoc.Api.Controllers;
using RegistroDoc.Api.Security;
using RegistroDoc.Api.Services;
using RegistroDoc.Domain.Entities;
using RegistroDoc.Infrastructure.Persistence;

namespace RegistroDoc.IntegrationTests;

[Collection("Auditoria HTTP")]
public sealed class DocumentAuditIntegrationTests
{
    [Fact]
    public async Task Pipeline_DeveAuditarPesquisaPdfRangeErrosENegadosSemConteudoSensivel()
    {
        var options = new DbContextOptionsBuilder<RegistroDocDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__RegistroDoc")).Options;
        await using var db = new RegistroDocDbContext(options);
        var root = Path.Combine(Path.GetTempPath(), "audit-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.4\nPDF FICTICIO PARA TESTE DE TRANSPORTE\n%%EOF");
        await File.WriteAllBytesAsync(Path.Combine(root, "ficticio.pdf"), bytes);
        var doc = new Documento { ServentiaId = await db.Serventias.Where(s => s.CodigoCns == "TESTE0001").Select(s => s.Id).SingleAsync(),
            NomeArquivo = "ficticio.pdf", CaminhoRelativo = "ficticio.pdf", HashSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            QuantidadePaginas = 1, StatusIndexacao = "Concluido", TipoDocumento = "AuditoriaFicticia", TamanhoBytes = bytes.Length };
        db.Documentos.Add(doc);
        db.PaginasDocumento.Add(new PaginaDocumento { DocumentoId = doc.Id, NumeroPagina = 1, TextoExtraido = "SEGREDO_FICTICIO_PESQUISA" });
        await db.SaveChangesAsync();
        var started = DateTime.UtcNow;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Documents:RootPath"] = root;
        builder.Services.AddDbContext<RegistroDocDbContext>(o => o.UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__RegistroDoc")));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddScoped<IAuditService, AuditService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(DocumentosController).Assembly);
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
            o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "audit-test",
                ValidateAudience = true, ValidAudience = "audit-test", IssuerSigningKey = key, ValidateIssuerSigningKey = true,
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero });
        builder.Services.AddAuthorization(o => o.AddPolicy("SomenteAdminTeste", p => p.RequireRole("Administrador")));
        await using var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseMiddleware<DocumentAuditMiddleware>();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/negado", () => "NAO_ENTREGAR").WithMetadata(new AuditarAcessoAttribute("TesteNegado"))
            .RequireAuthorization("SomenteAdminTeste");
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        var user = "ficticio-" + Guid.NewGuid();
        string Token(string actor, DateTime expires) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "audit-test", "audit-test", [new Claim(ClaimTypes.NameIdentifier, actor), new Claim(ClaimTypes.Role, "Consulta")],
            notBefore: expires.AddMinutes(-10), expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
        async Task<Auditoria> Check(string path, HttpStatusCode expected, string? token, string? range = null)
        {
            var before = await db.Auditorias.CountAsync(a => a.OcorridaEmUtc >= started);
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (range is not null) request.Headers.TryAddWithoutValidation("Range", range);
            using var response = await client.SendAsync(request);
            Assert.Equal(expected, response.StatusCode);
            if (expected is HttpStatusCode.OK or HttpStatusCode.PartialContent && path.EndsWith("/arquivo"))
                Assert.Equal(range is null ? bytes : bytes[..5], await response.Content.ReadAsByteArrayAsync());
            if (expected is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                Assert.DoesNotContain("%PDF", await response.Content.ReadAsStringAsync());
            Auditoria? record = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                var list = await db.Auditorias.AsNoTracking().Where(a => a.OcorridaEmUtc >= started).OrderBy(a => a.OcorridaEmUtc).ToListAsync();
                if (list.Count == before + 1 && !list[^1].Descricao!.Contains("Iniciado")) { record = list[^1]; break; }
                await Task.Delay(50);
            }
            Assert.NotNull(record);
            using var details = JsonDocument.Parse(record.Descricao!);
            Assert.Equal((int)expected, details.RootElement.GetProperty("StatusHttp").GetInt32());
            Assert.Equal(range is not null, details.RootElement.GetProperty("Parcial").GetBoolean());
            Assert.DoesNotContain("SEGREDO", record.Descricao);
            Assert.DoesNotContain(root, record.Descricao);
            Assert.DoesNotContain("Bearer", record.Descricao);
            return record;
        }
        try
        {
            var token = Token(user, DateTime.UtcNow.AddMinutes(5));
            var search = await Check("/api/documentos/pesquisa?Termo=SEGREDO_FICTICIO_PESQUISA", HttpStatusCode.OK, token);
            Assert.Equal(user, search.UsuarioId);
            Assert.Equal("PesquisarDocumentos", search.Acao);
            Assert.Equal("Pesquisa", search.EntidadeId);
            var pdfPath = $"/api/documentos/{doc.Id}/arquivo";
            var pdf = await Check(pdfPath, HttpStatusCode.OK, token);
            Assert.Equal(doc.Id.ToString(), pdf.EntidadeId);
            Assert.Equal("LerPdf", pdf.Acao);
            await Check(pdfPath, HttpStatusCode.PartialContent, token, "bytes=0-4");
            await Check(pdfPath, HttpStatusCode.RequestedRangeNotSatisfiable, token, "bytes=999999-");
            await Check($"/api/documentos/{Guid.NewGuid()}/arquivo", HttpStatusCode.NotFound, token);
            await Check("/api/documentos/pesquisa?AnoReferencia=invalido", HttpStatusCode.BadRequest, token);
            var denied = await Check("/negado", HttpStatusCode.Forbidden, token);
            Assert.Equal(user, denied.UsuarioId);
            Assert.Contains("Negado", denied.Descricao);
            Assert.Null((await Check(pdfPath, HttpStatusCode.Unauthorized, null)).UsuarioId);
            Assert.Null((await Check(pdfPath, HttpStatusCode.Unauthorized, "invalido")).UsuarioId);
            Assert.Null((await Check(pdfPath, HttpStatusCode.Unauthorized, Token(user, DateTime.UtcNow.AddMinutes(-1)))).UsuarioId);
            // Após remover a credencial (como ocorre depois do logout), o PDF permanece bloqueado.
            Assert.Null((await Check("/api/documentos/pesquisa", HttpStatusCode.Unauthorized, null)).UsuarioId);
            var other = "outro-" + Guid.NewGuid();
            Assert.Equal(other, (await Check("/api/documentos/pesquisa", HttpStatusCode.OK,
                Token(other, DateTime.UtcNow.AddMinutes(5)))).UsuarioId);
        }
        finally
        {
            await app.StopAsync();
            await db.Auditorias.Where(a => a.OcorridaEmUtc >= started).ExecuteDeleteAsync();
            await db.Documentos.Where(d => d.Id == doc.Id).ExecuteDeleteAsync();
            Directory.Delete(root, true);
        }
    }
}
