namespace RegistroDoc.Api.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    string? UserId { get; }

    string? Email { get; }

    bool IsInRole(string role);
}
