using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;

namespace PosTpv.Application.Services;

public interface IFloorZoneService
{
    Task<List<FloorZoneDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Draws a new "New zone" box near the middle of the floor plan, ready for the
    /// caller to drag/resize into place and rename.</summary>
    Task<FloorZoneDto> CreateAsync(CancellationToken ct = default);

    /// <summary>Updates a zone's name and accent colour together (the "Name this zone" modal
    /// edits both at once). Color null resets to the default neutral tint.</summary>
    Task UpdateAsync(int id, string name, string? color, CancellationToken ct = default);
    Task SaveLayoutAsync(IEnumerable<FloorZoneLayoutDto> layout, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class FloorZoneService : IFloorZoneService
{
    private readonly IUnitOfWork _uow;

    public FloorZoneService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<FloorZoneDto>> GetAllAsync(CancellationToken ct = default)
    {
        var zones = await _uow.Repository<FloorZone>().QueryNoTracking().OrderBy(z => z.Id).ToListAsync(ct);
        return zones.Select(ToDto).ToList();
    }

    public async Task<FloorZoneDto> CreateAsync(CancellationToken ct = default)
    {
        // Same fixed drop point as TableService/FloorDecorService — no stored canvas size to
        // compute a true center from.
        const double centerX = 300, centerY = 160;
        var entity = new FloorZone();
        entity.PositionX = centerX - entity.Width / 2;
        entity.PositionY = centerY - entity.Height / 2;
        entity.Name = "New zone";

        await _uow.Repository<FloorZone>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task UpdateAsync(int id, string name, string? color, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Zone name cannot be empty.");

        var entity = await _uow.Repository<FloorZone>().GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Zone {id} not found.");
        entity.Name = name.Trim();
        entity.Color = color;
        _uow.Repository<FloorZone>().Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task SaveLayoutAsync(IEnumerable<FloorZoneLayoutDto> layout, CancellationToken ct = default)
    {
        var repo = _uow.Repository<FloorZone>();
        var patches = layout.ToList();
        var ids = patches.Select(p => p.Id).ToList();
        var entities = await repo.Query().Where(z => ids.Contains(z.Id)).ToDictionaryAsync(z => z.Id, ct);

        foreach (var patch in patches)
        {
            if (!entities.TryGetValue(patch.Id, out var entity)) continue;
            entity.PositionX = patch.PositionX;
            entity.PositionY = patch.PositionY;
            entity.Width = patch.Width;
            entity.Height = patch.Height;
            repo.Update(entity);
        }
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<FloorZone>().GetByIdAsync(id, ct);
        if (entity is null) return;
        _uow.Repository<FloorZone>().Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private static FloorZoneDto ToDto(FloorZone z) =>
        new(z.Id, z.Name, z.PositionX, z.PositionY, z.Width, z.Height, z.Color);
}
