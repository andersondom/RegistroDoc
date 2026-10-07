using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RegistroDoc.Infrastructure.Persistence;

public sealed class RegistroDocDbContextFactory
    : IDesignTimeDbContextFactory<RegistroDocDbContext>
{
    public RegistroDocDbContext CreateDbContext(
        string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__RegistroDoc");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString =
                "Host=localhost;" +
                "Port=5432;" +
                "Database=registrodoc;" +
                "Username=registrodoc;" +
                "Password=DESIGN_TIME_ONLY";
        }

        var optionsBuilder =
            new DbContextOptionsBuilder<RegistroDocDbContext>();

        optionsBuilder.UseNpgsql(
            connectionString);

        return new RegistroDocDbContext(
            optionsBuilder.Options);
    }
}
