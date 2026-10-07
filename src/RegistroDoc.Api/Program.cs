using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RegistroDoc.Infrastructure.Persistence;
using RegistroDoc.Api.Security;
using RegistroDoc.Api.Services;
using RegistroDoc.Domain.Entities;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Serviços da API
// ============================================================

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuditService, AuditService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ============================================================
// PostgreSQL
// ============================================================

var connectionString =
    builder.Configuration.GetConnectionString("RegistroDoc");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "A conexão 'RegistroDoc' não foi configurada.");
}

builder.Services.AddDbContext<RegistroDocDbContext>(options =>
    options.UseNpgsql(connectionString));

// ============================================================
// JWT
// ============================================================

var jwtIssuer =
    builder.Configuration["Jwt:Issuer"];

var jwtAudience =
    builder.Configuration["Jwt:Audience"];

var jwtSigningKey =
    builder.Configuration["Jwt:SigningKey"];

if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    throw new InvalidOperationException(
        "Jwt:Issuer não foi configurado.");
}

if (string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "Jwt:Audience não foi configurado.");
}

if (string.IsNullOrWhiteSpace(jwtSigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey não foi configurado.");
}

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtSigningKey)),

                ValidateLifetime = true,

                ClockSkew = TimeSpan.FromSeconds(30)
            };
    });

builder.Services.AddAuthorization();

// ============================================================
// Aplicação
// ============================================================

var app = builder.Build();

// Em desenvolvimento, deixa um banco novo pronto para o fluxo
// fictício de validação sem depender de preparação manual.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<RegistroDocDbContext>();

    await db.Database.MigrateAsync();

    const string codigoCnsTeste = "TESTE0001";

    if (!await db.Serventias.AnyAsync(x => x.CodigoCns == codigoCnsTeste))
    {
        db.Serventias.Add(new Serventia
        {
            Id = Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Nome = "SERVENTIA FICTICIA - TESTE REGISTRODOC",
            CodigoCns = codigoCnsTeste,
            Municipio = "MUNICIPIO FICTICIO",
            Uf = "SE",
            Ativa = true,
            CriadaEmUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
}

// ============================================================
// Pipeline HTTP
// ============================================================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

