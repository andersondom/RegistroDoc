using System.ComponentModel.DataAnnotations;

namespace RegistroDoc.Contracts.Security;

public sealed record UserAccount(Guid Id, string Email, string NomeCompleto, bool Ativo, string[] Roles);
public sealed record CreateAccount(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(200)] string NomeCompleto,
    [Required, MinLength(10), MaxLength(128)] string Password,
    [Required] string Perfil);
public sealed record UpdateAccount([Required, MaxLength(200)] string NomeCompleto, bool Ativo, [Required] string Perfil);
public sealed record ResetAccountPassword([Required, MinLength(10), MaxLength(128)] string Password);
public sealed record ChangeOwnPassword([Required, MaxLength(128)] string CurrentPassword,
    [Required, MinLength(10), MaxLength(128)] string Password);
