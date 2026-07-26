using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Domain.Common;

namespace PosTpv.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of the generic repository.</summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly PosDbContext _db;
    private readonly DbSet<T> _set;

    public Repository(PosDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public IQueryable<T> Query() => _set.AsQueryable();

    public IQueryable<T> QueryNoTracking() => _set.AsNoTracking();

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await _set.AddAsync(entity, ct);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity) => _set.Remove(entity);
}
