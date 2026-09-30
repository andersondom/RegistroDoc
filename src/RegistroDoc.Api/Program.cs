using Microsoft.EntityFrameworkCore;
using RegistroDoc.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Serviços da API
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Conexão com o PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("RegistroDoc");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "A conexão 'RegistroDoc' não foi configurada.");
}

builder.Services.AddDbContext<RegistroDocDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
