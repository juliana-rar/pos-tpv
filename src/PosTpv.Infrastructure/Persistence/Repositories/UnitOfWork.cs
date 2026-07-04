using System.Collections.Concurrent;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Domain.Common;

namespace PosTpv.Infrastructure.Persistence.Repositories;

/// <summary>Shares a single DbContext across repositories and commits atomically.</summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly PosDbContext _db;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    public UnitOfWork(PosDbContext db) => _db = db;

    public IRepository<T> Repository<T>() where T : BaseEntity =>
        (IRepository<T>)_repositories.GetOrAdd(typeof(T), _ => new Repository<T>(_db));

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
