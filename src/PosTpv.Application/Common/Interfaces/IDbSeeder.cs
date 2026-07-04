namespace PosTpv.Application.Common.Interfaces;

/// <summary>Applies migrations and seeds baseline data (roles, demo catalogue, tables).</summary>
public interface IDbSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
