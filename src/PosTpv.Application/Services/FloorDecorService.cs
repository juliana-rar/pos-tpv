using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;
using PosTpv.Domain.Enums;

namespace PosTpv.Application.Services;

public interface IFloorDecorService
{
    Task<List<FloorDecorDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Drops a new decor element of the given type near the middle of the floor plan,
    /// sized by its type's default footprint, ready for the caller to drag into place.</summary>
    Task<FloorDecorDto> CreateAsync(FloorDecorType type, CancellationToken ct = default);
    Task SaveLayoutAsync(IEnumerable<FloorDecorLayoutDto> layout, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class FloorDecorService : IFloorDecorService
{
    private readonly IUnitOfWork _uow;

    public FloorDecorService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<FloorDecorDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await _uow.Repository<FloorDecor>().QueryNoTracking().ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<FloorDecorDto> CreateAsync(FloorDecorType type, CancellationToken ct = default)
    {
        var (width, height) = FloorDecor.DefaultSize(type);
        // Same fixed drop point as TableService.CreateAsync — no stored canvas size to compute a
        // true center from, so a fixed spot near the middle beats stacking every new item at 0,0.
        const double centerX = 300, centerY = 160;
        var entity = new FloorDecor
        {
            Type = type,
            Width = width,
            Height = height,
            PositionX = centerX - width / 2,
            PositionY = centerY - height / 2
        };

        await _uow.Repository<FloorDecor>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task SaveLayoutAsync(IEnumerable<FloorDecorLayoutDto> layout, CancellationToken ct = default)
    {
        var repo = _uow.Repository<FloorDecor>();
        var patches = layout.ToList();
        var ids = patches.Select(p => p.Id).ToList();
        var entities = await repo.Query().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);

        foreach (var patch in patches)
        {
            if (!entities.TryGetValue(patch.Id, out var entity)) continue;
            entity.PositionX = patch.PositionX;
            entity.PositionY = patch.PositionY;
            entity.Width = patch.Width;
            entity.Height = patch.Height;
            entity.Rotation = patch.Rotation;
            entity.IsLocked = patch.IsLocked;
            repo.Update(entity);
        }
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<FloorDecor>().GetByIdAsync(id, ct);
        if (entity is null) return;
        _uow.Repository<FloorDecor>().Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private static FloorDecorDto ToDto(FloorDecor d) =>
        new(d.Id, d.Type, d.PositionX, d.PositionY, d.Width, d.Height, d.Rotation, d.IsLocked);
}
