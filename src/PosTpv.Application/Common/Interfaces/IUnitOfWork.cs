using PosTpv.Domain.Common;

namespace PosTpv.Application.Common.Interfaces;

/// <summary>Coordinates repositories over a single DbContext and commits atomically.</summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a serializable transaction, retried per the
    /// provider's execution strategy. Use for read-then-write sequences that must not race
    /// (e.g. "create X only if it doesn't already exist").
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default);
}
