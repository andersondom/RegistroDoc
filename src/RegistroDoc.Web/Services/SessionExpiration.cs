namespace RegistroDoc.Web.Services;

public static class SessionExpiration
{
    public static DateTimeOffset Calculate(DateTime expiresAtUtc, DateTimeOffset now)
    {
        if (expiresAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("JWT expiration must be UTC.", nameof(expiresAtUtc));

        return new DateTimeOffset(expiresAtUtc) < now.AddMinutes(30)
            ? new DateTimeOffset(expiresAtUtc)
            : now.AddMinutes(30);
    }
}
