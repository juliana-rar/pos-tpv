using PosTpv.Domain.Common;

namespace PosTpv.Application.Common.Interfaces;

/// <summary>Generic repository (Repository Pattern). Query() returns a composable IQueryable.</summary>
public interface IRepository<T> where T : BaseEntity
{
    IQueryable<T> Query();

    /// <summary>Read-only query with EF Core change tracking disabled. Use for data that is only displayed, never mutated and saved.</summary>
    IQueryable<T> QueryNoTracking();
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
