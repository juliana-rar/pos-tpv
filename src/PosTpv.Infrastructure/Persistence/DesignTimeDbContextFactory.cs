using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PosTpv.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` create the context at design time without booting the web host
/// (so migrations never trigger the runtime seeding). Only used by EF tooling.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PosDbContext>
{
    public PosDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTPV_CONNECTION")
            ?? "Server=127.0.0.1,14333;Database=PosTpv;User Id=sa;Password=PosTpv!Dev2026;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new PosDbContext(options);
    }
}
