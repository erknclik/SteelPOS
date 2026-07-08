using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SanalPOS.Infrastructure.EfCore;

/// <summary>dotnet ef migrations komutları için design-time context factory.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SanalPosDbContext>
{
    public SanalPosDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Database=sanalpos;Username=sanalpos;Password=sanalpos_dev_pw";

        var options = new DbContextOptionsBuilder<SanalPosDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", SanalPosDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SanalPosDbContext(options);
    }
}
