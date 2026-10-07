using Microsoft.EntityFrameworkCore;
using RegistroDoc.Indexer;
using RegistroDoc.Indexer.Options;
using RegistroDoc.Indexer.Services;
using RegistroDoc.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<IndexerOptions>()
    .Bind(
        builder.Configuration.GetSection(
            IndexerOptions.SectionName))
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.InputPath),
        "Indexer:InputPath é obrigatório.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.SearchPattern),
        "Indexer:SearchPattern é obrigatório.")
    .Validate(
        options =>
            options.ScanIntervalSeconds > 0,
        "Indexer:ScanIntervalSeconds deve ser maior que zero.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(
                options.CodigoCnsServentia),
        "Indexer:CodigoCnsServentia é obrigatório.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(
                options.TipoDocumento),
        "Indexer:TipoDocumento é obrigatório.")
    .Validate(
    options =>
        options.ProcessingTimeoutMinutes > 0,
    "Indexer:ProcessingTimeoutMinutes deve ser maior que zero.")
.ValidateOnStart();

var connectionString =
    builder.Configuration.GetConnectionString("RegistroDoc");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:RegistroDoc não configurada.");
}

builder.Services.AddDbContext<RegistroDocDbContext>(
    options =>
        options.UseNpgsql(connectionString));

builder.Services.AddSingleton<
    IPdfFileInspector,
    PdfFileInspector>();

builder.Services.AddSingleton<
    IPdfTextExtractor,
    PdfPigTextExtractor>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();

