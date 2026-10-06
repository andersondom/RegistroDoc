using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RegistroDoc.IdentityHub.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDataProtection();

var connectionString =
    builder.Configuration.GetConnectionString("IdentityHub");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "A conexão 'IdentityHub' não foi configurada.");
}

builder.Services.AddDbContext<IdentityHubDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services
    .AddIdentityCore<IdentityHubUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<IdentityHubDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

var app = builder.Build();

await IdentityDataInitializer.InitializeAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();



