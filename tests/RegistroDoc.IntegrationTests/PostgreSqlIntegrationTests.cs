using Microsoft.EntityFrameworkCore;
using RegistroDoc.Infrastructure.Persistence;

namespace RegistroDoc.IntegrationTests;

public class PostgreSqlIntegrationTests
{
    [Fact]
    public async Task DeveConsultarServentiaFicticia()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__RegistroDoc");

        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<RegistroDocDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new RegistroDocDbContext(options);

        var serventia = await db.Serventias
            .AsNoTracking()
            .SingleOrDefaultAsync(
                s => s.CodigoCns == "TESTE0001");

        Assert.NotNull(serventia);

        Assert.Equal(
            "SERVENTIA FICTICIA - TESTE REGISTRODOC",
            serventia.Nome);
    }
}
