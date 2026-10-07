using Microsoft.AspNetCore.Identity;

namespace RegistroDoc.IdentityHub.Data;

public class IdentityHubUser : IdentityUser<Guid>
{
    public string NomeCompleto { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;
}
