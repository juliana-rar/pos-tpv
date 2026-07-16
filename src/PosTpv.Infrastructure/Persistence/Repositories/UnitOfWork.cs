using System.Data;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Domain.Common;

namespace PosTpv.Infrastructure.Persistence.Repositories;

/// <summary>Shares a single DbContext across repositories and commits atomically.</summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly PosDbContext _db;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(PosDbContext db) => _db = db;

    // DbContext (and this UnitOfWork) is registered Scoped: one instance per request/circuit,
    // used from a single logical thread of execution — no concurrent access to guard against here.
    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        if (!_repositories.TryGetValue(typeof(T), out var repo))
        {
            repo = new Repository<T>(_db);
            _repositories[typeof(T)] = repo;
        }
        return (IRepository<T>)repo;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        // Sqlite has no configurable isolation level (writers are serialized by the engine itself);
        // only ask for Serializable against providers that support it (e.g. SQL Server).
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(ct, async cancellationToken =>
        {
            var isSqlite = _db.Database.IsSqlite();
            await using var transaction = isSqlite
                ? await _db.Database.BeginTransactionAsync(cancellationToken)
                : await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
