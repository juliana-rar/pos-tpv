using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;
using PosTpv.Domain.Enums;

namespace PosTpv.Application.Services;

public interface IReservationService
{
    Task<List<ReservationDto>> GetByDateAsync(DateTime date, CancellationToken ct = default);
    Task<List<ReservationDto>> GetUpcomingAsync(CancellationToken ct = default);
    /// <summary>Every reservation ever made, newest first — backs the full reservations dashboard (today/upcoming/past/stats).</summary>
    Task<List<ReservationDto>> GetAllAsync(CancellationToken ct = default);
    Task<ReservationFormDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(ReservationFormDto form, CancellationToken ct = default);
    Task UpdateAsync(ReservationFormDto form, CancellationToken ct = default);
    /// <summary>Replaces the full set of tables assigned to a reservation.</summary>
    Task AssignTablesAsync(int reservationId, IEnumerable<int> tableIds, CancellationToken ct = default);
    /// <summary>Adds or removes a single table from a reservation's assignment (used by the floor-plan click-to-assign flow).</summary>
    Task ToggleTableAsync(int reservationId, int tableId, CancellationToken ct = default);
    Task SetStatusAsync(int id, ReservationStatus status, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class ReservationService : IReservationService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ITableService _tableService;

    public ReservationService(IUnitOfWork uow, IMapper mapper, ITableService tableService)
    {
        _uow = uow;
        _mapper = mapper;
        _tableService = tableService;
    }

    private IQueryable<Reservation> WithTable() =>
        _uow.Repository<Reservation>().Query().Include(r => r.Tables);

    public async Task<List<ReservationDto>> GetByDateAsync(DateTime date, CancellationToken ct = default)
    {
        var list = await WithTable()
            .Where(r => r.Date == date.Date)
            .OrderBy(r => r.Time).ToListAsync(ct);
        return _mapper.Map<List<ReservationDto>>(list);
    }

    public async Task<List<ReservationDto>> GetUpcomingAsync(CancellationToken ct = default)
    {
        var list = await WithTable()
            .Where(r => r.Date >= DateTime.Today && r.Status != ReservationStatus.Cancelled)
            .OrderBy(r => r.Date).ThenBy(r => r.Time).ToListAsync(ct);
        return _mapper.Map<List<ReservationDto>>(list);
    }

    public async Task<List<ReservationDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await WithTable()
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.Time).ToListAsync(ct);
        return _mapper.Map<List<ReservationDto>>(list);
    }

    public async Task<ReservationFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var entity = await WithTable().FirstOrDefaultAsync(r => r.Id == id, ct);
        return entity is null ? null : _mapper.Map<ReservationFormDto>(entity);
    }

    public async Task<int> CreateAsync(ReservationFormDto form, CancellationToken ct = default)
    {
        var entity = _mapper.Map<Reservation>(form);
        entity.Id = 0;
        entity.Tables = await ResolveTablesAsync(form.TableIds, ct);
        await _uow.Repository<Reservation>().AddAsync(entity, ct);
        await SyncTableStatusAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await AutoJoinIfMultipleAsync(entity, ct);
        return entity.Id;
    }

    public async Task UpdateAsync(ReservationFormDto form, CancellationToken ct = default)
    {
        var entity = await WithTable().FirstOrDefaultAsync(r => r.Id == form.Id, ct)
            ?? throw new KeyNotFoundException($"Reservation {form.Id} not found.");
        _mapper.Map(form, entity);
        entity.Tables = await ResolveTablesAsync(form.TableIds, ct);
        _uow.Repository<Reservation>().Update(entity);
        await SyncTableStatusAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await AutoJoinIfMultipleAsync(entity, ct);
    }

    public async Task AssignTablesAsync(int reservationId, IEnumerable<int> tableIds, CancellationToken ct = default)
    {
        var entity = await WithTable().FirstOrDefaultAsync(r => r.Id == reservationId, ct)
            ?? throw new KeyNotFoundException($"Reservation {reservationId} not found.");
        entity.Tables = await ResolveTablesAsync(tableIds, ct);
        _uow.Repository<Reservation>().Update(entity);
        await SyncTableStatusAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await AutoJoinIfMultipleAsync(entity, ct);
    }

    public async Task ToggleTableAsync(int reservationId, int tableId, CancellationToken ct = default)
    {
        var entity = await WithTable().FirstOrDefaultAsync(r => r.Id == reservationId, ct)
            ?? throw new KeyNotFoundException($"Reservation {reservationId} not found.");
        var existing = entity.Tables.FirstOrDefault(t => t.Id == tableId);
        if (existing is not null)
        {
            entity.Tables.Remove(existing);
        }
        else
        {
            var table = await _uow.Repository<RestaurantTable>().GetByIdAsync(tableId, ct);
            if (table is not null) entity.Tables.Add(table);
        }
        _uow.Repository<Reservation>().Update(entity);
        await SyncTableStatusAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await AutoJoinIfMultipleAsync(entity, ct);
    }

    // A reservation spanning two or more tables should read as one physical table on the
    // floor plan, so joining them is automatic instead of requiring a separate manual step.
    private async Task AutoJoinIfMultipleAsync(Reservation entity, CancellationToken ct)
    {
        var tableIds = entity.Tables.Select(t => t.Id).ToList();
        if (tableIds.Count < 2) return;
        try { await _tableService.JoinTablesAsync(tableIds, ct: ct); }
        catch (InvalidOperationException) { /* e.g. a member table has an open order — leave grouping to the user */ }
    }

    public async Task SetStatusAsync(int id, ReservationStatus status, CancellationToken ct = default)
    {
        var entity = await WithTable().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return;

        if (status == ReservationStatus.Seated && entity.Tables.Count == 0)
            throw new InvalidOperationException("Assign a table before seating this reservation.");

        entity.Status = status;
        _uow.Repository<Reservation>().Update(entity);
        await SyncTableStatusAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Reservation>().GetByIdAsync(id, ct);
        if (entity is null) return;
        _uow.Repository<Reservation>().Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<List<RestaurantTable>> ResolveTablesAsync(IEnumerable<int> tableIds, CancellationToken ct)
    {
        var ids = tableIds.Distinct().ToList();
        if (ids.Count == 0) return new List<RestaurantTable>();
        return await _uow.Repository<RestaurantTable>().Query().Where(t => ids.Contains(t.Id)).ToListAsync(ct);
    }

    // Frees each of the reservation's tables back to Available, unless another still-active
    // reservation also claims it (tables are only ever marked Reserved, never Occupied, by
    // reservation logic, so it's always safe to downgrade — an active order's Occupied status
    // is left untouched and is only cleared on full payment, in OrderService).
    private async Task ReleaseTablesIfFreeAsync(Reservation reservation, CancellationToken ct)
    {
        foreach (var table in reservation.Tables.Where(t => t.Status == TableStatus.Reserved))
        {
            var stillNeeded = await _uow.Repository<Reservation>().Query().AnyAsync(r =>
                r.Id != reservation.Id &&
                r.Tables.Any(t2 => t2.Id == table.Id) &&
                r.Status != ReservationStatus.Finished && r.Status != ReservationStatus.Cancelled, ct);
            if (!stillNeeded)
            {
                table.Status = TableStatus.Available;
                _uow.Repository<RestaurantTable>().Update(table);
            }
        }
    }

    private void MarkTableReservedIfNeeded(Reservation reservation)
    {
        if (reservation.Status is ReservationStatus.Finished or ReservationStatus.Cancelled) return;

        foreach (var table in reservation.Tables.Where(t => t.Status == TableStatus.Available))
        {
            table.Status = TableStatus.Reserved;
            _uow.Repository<RestaurantTable>().Update(table);
        }
    }

    // Single entry point for keeping a reservation's tables in sync with its current status:
    // Finished/Cancelled releases them back to Available (unless another active reservation
    // still needs them), anything else marks still-Available tables as Reserved. Every mutation
    // path (create, update, assign, toggle, status change) funnels through this so a table can
    // never get stuck Reserved after its reservation is cancelled or finished.
    private async Task SyncTableStatusAsync(Reservation reservation, CancellationToken ct)
    {
        if (reservation.Status is ReservationStatus.Finished or ReservationStatus.Cancelled)
            await ReleaseTablesIfFreeAsync(reservation, ct);
        else
            MarkTableReservedIfNeeded(reservation);
    }
}
