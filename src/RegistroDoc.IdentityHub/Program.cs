using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RegistroDoc.IdentityHub.Data;
using RegistroDoc.IdentityHub.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDataProtection();

//
// PostgreSQL / IdentityHub
//

var connectionString =
    builder.Configuration.GetConnectionString("IdentityHub");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "A conexão 'IdentityHub' não foi configurada.");
}

builder.Services.AddDbContext<IdentityHubDbContext>(options =>
    options.UseNpgsql(connectionString));

//
// ASP.NET Core Identity
//

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

//
// JWT
//

var jwtOptions = new JwtOptions();

builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Bind(jwtOptions);

if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
{
    throw new InvalidOperationException(
        "Jwt:Issuer não foi configurado.");
}

if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
{
    throw new InvalidOperationException(
        "Jwt:Audience não foi configurado.");
}

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey não foi configurado.");
}

if (jwtOptions.ExpirationMinutes <= 0)
{
    throw new InvalidOperationException(
        "Jwt:ExpirationMinutes deve ser maior que zero.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtOptions.SigningKey)),

                ValidateLifetime = true,

                ClockSkew = TimeSpan.FromSeconds(30)
            };
    });

builder.Services.AddAuthorization();

//
// Serviços do IdentityHub
//

builder.Services.AddScoped<JwtTokenService>();

//
// Aplicação
//

var app = builder.Build();

// Garante que um ambiente novo tenha o schema do ASP.NET Core Identity
// antes da criação das roles e do administrador inicial.
using (var scope = app.Services.CreateScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<IdentityHubDbContext>();

    await dbContext.Database.MigrateAsync();
}

await IdentityDataInitializer.InitializeAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();