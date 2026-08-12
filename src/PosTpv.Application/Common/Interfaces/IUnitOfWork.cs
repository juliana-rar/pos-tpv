using PosTpv.Domain.Common;

namespace PosTpv.Application.Common.Interfaces;

/// <summary>Coordinates repositories over a single DbContext and commits atomically.</summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// When true, suppresses the CreatedAt/UpdatedAt auto-stamp on the next SaveChangesAsync call.
    /// For edits that shouldn't count as "this line was touched" — e.g. a quantity bump on an
    /// already-served order line, which must not disturb the UpdatedAt timestamp that the
    /// served-batch grouping (OrderPresentation.GroupByServeBatch) and "served at" labels key off.
    /// Caller is responsible for resetting it to false once done (this context is request-scoped
    /// and reused across calls).
    /// </summary>
    bool SkipAuditStamp { get; set; }

    /// <summary>
    /// Runs <paramref name="operation"/> inside a serializable transaction, retried per the
    /// provider's execution strategy. Use for read-then-write sequences that must not race
    /// (e.g. "create X only if it doesn't already exist").
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default);
}
