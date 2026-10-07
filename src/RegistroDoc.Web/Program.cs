using RegistroDoc.Web.Components;
using RegistroDoc.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddTransient<ApiTokenHandler>();

var apiBaseUrl =
    builder.Configuration["Api:BaseUrl"]
    ?? "http://localhost:8082/";

builder.Services.AddHttpClient(
        "RegistroDoc.Api",
        client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<ApiTokenHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute(
    "/not-found",
    createScopeForStatusCodePages: true);

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
