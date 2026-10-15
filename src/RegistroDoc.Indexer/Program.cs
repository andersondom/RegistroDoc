using Microsoft.EntityFrameworkCore;
using RegistroDoc.Indexer;
using RegistroDoc.Indexer.Options;
using RegistroDoc.Indexer.Services;
using RegistroDoc.Infrastructure.Persistence;
using RegistroDoc.Indexer.Importing;
using System.Text.Json;

var plan = args.Contains("--import-plan");
var execute = args.Contains("--import-execute");
if (plan && execute)
{
    Console.Error.WriteLine("Escolha simulação ou execução.");
    Environment.ExitCode = 1;
    return;
}
var importMode = plan || execute;
var hostArgs = args.Where(a => a is not "--import-plan" and not "--import-execute").ToArray();
var builder = Host.CreateApplicationBuilder(hostArgs);
if (importMode && builder.Configuration["Importacao:ConfigPath"] is { Length: > 0 } configPath)
{
    try { builder.Configuration.AddJsonFile(Path.GetFullPath(configPath), optional: false)
        .AddEnvironmentVariables().AddCommandLine(hostArgs); }
    catch (Exception)
    {
        Console.Error.WriteLine("Não foi possível ler a configuração local de importação.");
        Environment.ExitCode = 1;
        return;
    }
}

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

if ((!importMode || execute) && string.IsNullOrWhiteSpace(connectionString))
{
    if (importMode)
    {
        Console.Error.WriteLine("Configure a conexão com o banco autorizado antes de executar a carga.");
        Environment.ExitCode = 1;
        return;
    }
    throw new InvalidOperationException(
        "ConnectionStrings:RegistroDoc não configurada.");
}

if (!string.IsNullOrWhiteSpace(connectionString)) builder.Services.AddDbContext<RegistroDocDbContext>(
    options =>
        options.UseNpgsql(connectionString));

builder.Services.AddSingleton<
    IPdfFileInspector,
    PdfFileInspector>();

builder.Services.AddSingleton<
    IPdfTextExtractor,
    PdfPigTextExtractor>();

if (!importMode) builder.Services.AddHostedService<Worker>();
else builder.Services.AddSingleton<PdfBatchImporter>();

using var host = builder.Build();

if (importMode)
{
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
    try
    {
        var options = builder.Configuration.GetSection("Importacao").Get<ImportOptions>() ?? new();
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var database = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
            // Identidade do destino sem senha; a simulação continua sem acessar o banco.
            options.DatabaseIdentity = JsonSerializer.Serialize(new { database.Host, database.Port,
                database.Database, database.Username, database.SearchPath });
        }
        else options.DatabaseIdentity = "SemConexaoConfigurada";
        var summary = await host.Services.GetRequiredService<PdfBatchImporter>().RunAsync(options, execute, cancellation.Token);
        Console.WriteLine(JsonSerializer.Serialize(summary));
        Environment.ExitCode = summary.Falhas == 0 ? 0 : 1;
    }
    catch (OperationCanceledException) { Console.Error.WriteLine("Importação cancelada; relatório parcial preservado."); Environment.ExitCode = 130; }
    catch (ImportPlanException) { Console.Error.WriteLine("Plano ausente, incompleto ou divergente. Gere e revise uma nova simulação antes da carga."); Environment.ExitCode = 1; }
    catch (Exception) { Console.Error.WriteLine("Não foi possível iniciar ou concluir a importação. Confira a configuração e o relatório privado."); Environment.ExitCode = 1; }
}
else await host.RunAsync();

