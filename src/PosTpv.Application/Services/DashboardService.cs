using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;
using PosTpv.Domain.Enums;

namespace PosTpv.Application.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
}

public class DashboardService : IDashboardService
{
    private static readonly OrderStatus[] ActiveStatuses =
        { OrderStatus.Open, OrderStatus.Sent, OrderStatus.InPreparation, OrderStatus.Ready, OrderStatus.Delivered };

    private readonly IUnitOfWork _uow;

    public DashboardService(IUnitOfWork uow) => _uow = uow;

    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var invoices = _uow.Repository<Invoice>().Query();
        var salesToday = await invoices.Where(i => i.CreatedAt >= today).SumAsync(i => (decimal?)i.Total, ct) ?? 0m;
        var salesMonth = await invoices.Where(i => i.CreatedAt >= monthStart).SumAsync(i => (decimal?)i.Total, ct) ?? 0m;

        var orders = _uow.Repository<Order>().Query();
        var ordersToday = await orders.CountAsync(o => o.CreatedAt >= today, ct);
        var openOrders = await orders.CountAsync(o => ActiveStatuses.Contains(o.Status), ct);

        var tables = _uow.Repository<RestaurantTable>().Query();
        var totalTables = await tables.CountAsync(ct);
        var occupiedTables = await tables.CountAsync(t => t.Status == TableStatus.Occupied, ct);

        var reservationsToday = await _uow.Repository<Reservation>().Query()
            .CountAsync(r => r.Date == today && r.Status != ReservationStatus.Cancelled, ct);

        var topRaw = await _uow.Repository<OrderItem>().Query()
            .Where(i => i.Order.Status == OrderStatus.Paid)
            .GroupBy(i => i.Product.Name)
            .Select(g => new { Name = g.Key, Qty = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.UnitPrice * i.Quantity) })
            .OrderByDescending(x => x.Qty)
            .Take(5)
            .ToListAsync(ct);
        var topProducts = topRaw.Select(x => new TopProductDto(x.Name, x.Qty, x.Revenue)).ToList();

        return new DashboardDto(salesToday, salesMonth, ordersToday, openOrders,
            occupiedTables, totalTables, reservationsToday, topProducts);
    }
}
