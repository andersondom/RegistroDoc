namespace RegistroDoc.Web.Services;

public sealed class UserSession
{
    public string? AccessToken { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public string? Email { get; private set; }
    public string? NomeCompleto { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(AccessToken) &&
        ExpiresAtUtc > DateTime.UtcNow;

    public void SignIn(
        string accessToken,
        DateTime expiresAtUtc,
        string email,
        string nomeCompleto,
        IReadOnlyList<string> roles)
    {
        AccessToken = accessToken;
        ExpiresAtUtc = expiresAtUtc;
        Email = email;
        NomeCompleto = nomeCompleto;
        Roles = roles;
    }

    public void SignOut()
    {
        AccessToken = null;
        ExpiresAtUtc = null;
        Email = null;
        NomeCompleto = null;
        Roles = [];
    }
}
