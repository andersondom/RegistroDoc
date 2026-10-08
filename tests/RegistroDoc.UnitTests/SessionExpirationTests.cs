using RegistroDoc.Web.Services;

namespace RegistroDoc.UnitTests;

public sealed class SessionExpirationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void JwtExpiringSoonLimitsSession()
    {
        var expires = Now.AddMinutes(10);
        Assert.Equal(expires, SessionExpiration.Calculate(expires.UtcDateTime, Now));
    }

    [Fact]
    public void LongJwtCannotExtendSessionBeyondThirtyMinutes()
    {
        var expires = Now.AddHours(2);
        Assert.Equal(Now.AddMinutes(30), SessionExpiration.Calculate(expires.UtcDateTime, Now));
    }

    [Fact]
    public void ExpiredJwtDoesNotGrantAdditionalTime()
    {
        var expires = Now.AddMinutes(-1);
        Assert.Equal(expires, SessionExpiration.Calculate(expires.UtcDateTime, Now));
    }

    [Fact]
    public void NonUtcJwtExpirationIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionExpiration.Calculate(DateTime.SpecifyKind(Now.DateTime, DateTimeKind.Unspecified), Now));
    }
}
