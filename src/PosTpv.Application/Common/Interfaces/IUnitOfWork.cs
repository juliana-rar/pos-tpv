using PosTpv.Domain.Common;

namespace PosTpv.Application.Common.Interfaces;

/// <summary>Coordinates repositories over a single DbContext and commits atomically.</summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
