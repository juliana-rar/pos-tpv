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
    Task<ReservationFormDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(ReservationFormDto form, CancellationToken ct = default);
    Task UpdateAsync(ReservationFormDto form, CancellationToken ct = default);
    Task AssignTableAsync(int reservationId, int? tableId, CancellationToken ct = default);
    Task SetStatusAsync(int id, ReservationStatus status, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class ReservationService : IReservationService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ReservationService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    private IQueryable<Reservation> WithTable() =>
        _uow.Repository<Reservation>().Query().Include(r => r.Table);

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

    public async Task<ReservationFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Reservation>().GetByIdAsync(id, ct);
        return entity is null ? null : _mapper.Map<ReservationFormDto>(entity);
    }

    public async Task<int> CreateAsync(ReservationFormDto form, CancellationToken ct = default)
    {
        var entity = _mapper.Map<Reservation>(form);
        entity.Id = 0;
        await _uow.Repository<Reservation>().AddAsync(entity, ct);
        await MarkTableReservedIfNeeded(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(ReservationFormDto form, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Reservation>().GetByIdAsync(form.Id, ct)
            ?? throw new KeyNotFoundException($"Reservation {form.Id} not found.");
        _mapper.Map(form, entity);
        _uow.Repository<Reservation>().Update(entity);
        await MarkTableReservedIfNeeded(entity, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task AssignTableAsync(int reservationId, int? tableId, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Reservation>().GetByIdAsync(reservationId, ct)
            ?? throw new KeyNotFoundException($"Reservation {reservationId} not found.");
        entity.TableId = tableId;
        _uow.Repository<Reservation>().Update(entity);
        await MarkTableReservedIfNeeded(entity, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task SetStatusAsync(int id, ReservationStatus status, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Reservation>().GetByIdAsync(id, ct);
        if (entity is null) return;
        entity.Status = status;
        _uow.Repository<Reservation>().Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Reservation>().GetByIdAsync(id, ct);
        if (entity is null) return;
        _uow.Repository<Reservation>().Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task MarkTableReservedIfNeeded(Reservation reservation, CancellationToken ct)
    {
        if (reservation.TableId is null) return;
        if (reservation.Status is ReservationStatus.Finished or ReservationStatus.Cancelled) return;

        var table = await _uow.Repository<RestaurantTable>().GetByIdAsync(reservation.TableId.Value, ct);
        if (table is not null && table.Status == TableStatus.Available)
        {
            table.Status = TableStatus.Reserved;
            _uow.Repository<RestaurantTable>().Update(table);
        }
    }
}
