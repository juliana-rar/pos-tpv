using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;
using PosTpv.Domain.Enums;

namespace PosTpv.Application.Services;

public interface ITableService
{
    Task<List<TableDto>> GetAllAsync(CancellationToken ct = default);
    Task<int> CreateAsync(TableFormDto form, CancellationToken ct = default);
    Task UpdateInfoAsync(TableFormDto form, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task SaveLayoutAsync(IEnumerable<TableLayoutDto> layout, CancellationToken ct = default);
    Task JoinTablesAsync(IEnumerable<int> tableIds, string? groupName = null, CancellationToken ct = default);
    Task SeparateGroupAsync(int tableId, CancellationToken ct = default);
    Task RenameGroupAsync(int groupId, string groupName, CancellationToken ct = default);
}

public class TableService : ITableService
{
    private static readonly OrderStatus[] ActiveStatuses =
        { OrderStatus.Open, OrderStatus.Sent, OrderStatus.InPreparation, OrderStatus.Ready, OrderStatus.Delivered };

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public TableService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<List<TableDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tables = await _uow.Repository<RestaurantTable>().Query()
            .OrderBy(t => t.Name).ToListAsync(ct);

        var activeOrders = await _uow.Repository<Order>().Query()
            .Where(o => ActiveStatuses.Contains(o.Status))
            .Include(o => o.Items).ThenInclude(i => i.Extras)
            .ToListAsync(ct);

        var byTable = activeOrders.GroupBy(o => o.TableId).ToDictionary(g => g.Key, g => g.First());

        // A joined group's order lives on its primary table; surface it on every member.
        var tableGroup = tables.Where(t => t.GroupId is not null).ToDictionary(t => t.Id, t => t.GroupId!.Value);
        var byGroup = activeOrders
            .Where(o => tableGroup.ContainsKey(o.TableId))
            .GroupBy(o => tableGroup[o.TableId])
            .ToDictionary(g => g.Key, g => g.First());

        return tables.Select(t =>
        {
            if (!byTable.TryGetValue(t.Id, out var order) && t.GroupId is not null)
                byGroup.TryGetValue(t.GroupId.Value, out order);

            return new TableDto(t.Id, t.Name, t.Seats, t.Shape, t.Status,
                t.PositionX, t.PositionY, t.Width, t.Height, t.Rotation, t.IsLocked,
                order?.Id, order?.Total ?? 0m, t.GroupId, t.Zone, t.GroupName, t.Color);
        }).ToList();
    }

    public async Task<int> CreateAsync(TableFormDto form, CancellationToken ct = default)
    {
        var entity = new RestaurantTable
        {
            Name = form.Name,
            Seats = form.Seats,
            Shape = form.Shape,
            Zone = form.Zone,
            Color = form.Color,
        };

        // Land the new table near the middle of the floor plan instead of the top-left corner
        // (position 0,0), so it doesn't spawn stacked under the toolbar out of sight. This is a
        // fixed point rather than an average of existing tables: the floor has no stored canvas
        // size to compute a true center from, and averaging would drift toward whatever's already
        // there (including any table still sitting at a stale 0,0).
        const double centerX = 300, centerY = 160;
        entity.PositionX = centerX - entity.Width / 2;
        entity.PositionY = centerY - entity.Height / 2;

        await _uow.Repository<RestaurantTable>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateInfoAsync(TableFormDto form, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<RestaurantTable>().GetByIdAsync(form.Id, ct)
            ?? throw new KeyNotFoundException($"Table {form.Id} not found.");
        entity.Name = form.Name;
        entity.Seats = form.Seats;
        entity.Shape = form.Shape;
        entity.Zone = form.Zone;
        entity.Color = form.Color;
        _uow.Repository<RestaurantTable>().Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<RestaurantTable>().GetByIdAsync(id, ct);
        if (entity is null) return;

        var hasActive = await _uow.Repository<Order>().Query()
            .AnyAsync(o => o.TableId == id && ActiveStatuses.Contains(o.Status), ct);
        if (hasActive)
            throw new InvalidOperationException("Cannot delete a table with an open order.");

        _uow.Repository<RestaurantTable>().Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task SaveLayoutAsync(IEnumerable<TableLayoutDto> layout, CancellationToken ct = default)
    {
        var repo = _uow.Repository<RestaurantTable>();
        foreach (var patch in layout)
        {
            var entity = await repo.GetByIdAsync(patch.Id, ct);
            if (entity is null) continue;
            entity.PositionX = patch.PositionX;
            entity.PositionY = patch.PositionY;
            entity.Width = patch.Width;
            entity.Height = patch.Height;
            entity.Rotation = patch.Rotation;
            entity.IsLocked = patch.IsLocked;
            entity.Shape = patch.Shape;
            repo.Update(entity);
        }
        await _uow.SaveChangesAsync(ct);
    }

    public async Task JoinTablesAsync(IEnumerable<int> tableIds, string? groupName = null, CancellationToken ct = default)
    {
        var ids = tableIds.Distinct().ToList();
        if (ids.Count < 2)
            throw new InvalidOperationException("Select at least two tables to join.");

        var repo = _uow.Repository<RestaurantTable>();
        var selected = await repo.Query().Where(t => ids.Contains(t.Id)).ToListAsync(ct);

        // Pull in any tables already grouped with the selection so groups merge cleanly.
        var touchedGroups = selected.Where(t => t.GroupId is not null).Select(t => t.GroupId!.Value).Distinct().ToList();
        var members = await repo.Query()
            .Where(t => ids.Contains(t.Id) || (t.GroupId != null && touchedGroups.Contains(t.GroupId.Value)))
            .ToListAsync(ct);

        var memberIds = members.Select(t => t.Id).ToList();
        var hasActive = await _uow.Repository<Order>().Query()
            .AnyAsync(o => memberIds.Contains(o.TableId) && ActiveStatuses.Contains(o.Status), ct);
        if (hasActive)
            throw new InvalidOperationException("Cannot join tables with an open order. Charge them first.");

        var newGroupId = (await repo.Query().MaxAsync(t => (int?)t.GroupId, ct) ?? 0) + 1;
        var resolvedName = string.IsNullOrWhiteSpace(groupName)
            ? string.Join(" + ", members.OrderBy(t => t.Name).Select(t => t.Name))
            : groupName.Trim();

        // Snap the members edge-to-edge into a single contiguous row so the group
        // reads as one physical table. The top-left-most member is the anchor; the
        // rest line up to its right, sharing its top edge and height. A 2px overlap
        // collapses the neighbouring borders into a single seam.
        const double borderOverlap = 2;
        var ordered = members.OrderBy(t => t.PositionX).ThenBy(t => t.PositionY).ToList();
        var anchor = ordered[0];
        double cursorX = anchor.PositionX;
        foreach (var t in ordered)
        {
            // Snapshot the original disposition once (only the first time a table is joined),
            // so merging groups keeps the earliest layout and Separate can restore it.
            if (t.PreJoinPositionX is null)
            {
                t.PreJoinPositionX = t.PositionX;
                t.PreJoinPositionY = t.PositionY;
                t.PreJoinHeight = t.Height;
                t.PreJoinRotation = t.Rotation;
                t.PreJoinIsLocked = t.IsLocked;
            }

            t.PositionY = anchor.PositionY;
            t.Height = anchor.Height;
            t.Rotation = 0;
            t.IsLocked = false;
            t.PositionX = cursorX;
            cursorX += t.Width - borderOverlap;

            t.GroupId = newGroupId;
            t.GroupName = resolvedName;
            repo.Update(t);
        }
        await _uow.SaveChangesAsync(ct);
    }

    public async Task RenameGroupAsync(int groupId, string groupName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            throw new InvalidOperationException("Group name cannot be empty.");

        var repo = _uow.Repository<RestaurantTable>();
        var members = await repo.Query().Where(t => t.GroupId == groupId).ToListAsync(ct);
        foreach (var t in members)
        {
            t.GroupName = groupName.Trim();
            repo.Update(t);
        }
        await _uow.SaveChangesAsync(ct);
    }

    public async Task SeparateGroupAsync(int tableId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<RestaurantTable>();
        var table = await repo.GetByIdAsync(tableId, ct);
        if (table?.GroupId is null) return;

        var members = await repo.Query().Where(t => t.GroupId == table.GroupId).ToListAsync(ct);
        var memberIds = members.Select(t => t.Id).ToList();

        var hasActive = await _uow.Repository<Order>().Query()
            .AnyAsync(o => memberIds.Contains(o.TableId) && ActiveStatuses.Contains(o.Status), ct);
        if (hasActive)
            throw new InvalidOperationException("Charge the joined tables before separating them.");

        foreach (var t in members)
        {
            // Restore the disposition captured when the table was joined, then clear the snapshot.
            if (t.PreJoinPositionX is not null)
            {
                t.PositionX = t.PreJoinPositionX.Value;
                t.PositionY = t.PreJoinPositionY!.Value;
                t.Height = t.PreJoinHeight!.Value;
                t.Rotation = t.PreJoinRotation!.Value;
                t.IsLocked = t.PreJoinIsLocked ?? false;
            }

            t.PreJoinPositionX = null;
            t.PreJoinPositionY = null;
            t.PreJoinHeight = null;
            t.PreJoinRotation = null;
            t.PreJoinIsLocked = null;
            t.GroupId = null;
            t.GroupName = null;
            repo.Update(t);
        }
        await _uow.SaveChangesAsync(ct);
    }
}
