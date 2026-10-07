using Microsoft.AspNetCore.Identity;
using RegistroDoc.IdentityHub.Security;

namespace RegistroDoc.IdentityHub.Data;

public static class IdentityDataInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var roleManager =
            scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<UserManager<IdentityHubUser>>();

        string[] roles =
        [
            IdentityRoles.Administrador,
            IdentityRoles.Operador
        ];

        foreach (var roleName in roles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(
                new IdentityRole<Guid>(roleName));

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    FormatErrors(
                        $"Não foi possível criar a role '{roleName}'",
                        roleResult.Errors));
            }
        }

        var adminEmail =
            Environment.GetEnvironmentVariable(
                "IDENTITY_ADMIN_EMAIL");

        var adminPassword =
            Environment.GetEnvironmentVariable(
                "IDENTITY_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(adminEmail) ||
            string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin is null)
        {
            admin = new IdentityHubUser
            {
                Id = Guid.NewGuid(),
                UserName = adminEmail,
                Email = adminEmail,
                NomeCompleto = "Administrador",
                Ativo = true,
                EmailConfirmed = true
            };

            var createResult =
                await userManager.CreateAsync(
                    admin,
                    adminPassword);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    FormatErrors(
                        "Não foi possível criar o Administrador",
                        createResult.Errors));
            }
        }

        if (!await userManager.IsInRoleAsync(
                admin,
                IdentityRoles.Administrador))
        {
            var roleResult =
                await userManager.AddToRoleAsync(
                    admin,
                    IdentityRoles.Administrador);

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    FormatErrors(
                        "Não foi possível atribuir a role Administrador",
                        roleResult.Errors));
            }
        }
    }

    private static string FormatErrors(
        string message,
        IEnumerable<IdentityError> errors)
    {
        var details = string.Join(
            "; ",
            errors.Select(error => error.Description));

        return $"{message}: {details}";
    }
}
