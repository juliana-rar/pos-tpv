using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;
using PosTpv.Domain.Enums;

namespace PosTpv.Application.Services;

public interface IOrderService
{
    Task<List<OrderDto>> GetOpenOrdersAsync(CancellationToken ct = default);
    Task<OrderDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<OrderDto?> GetActiveByTableAsync(int tableId, CancellationToken ct = default);
    Task<OrderDto> OpenOrderAsync(int tableId, int waiterId, CancellationToken ct = default);
    Task<OrderDto> AddItemAsync(AddItemRequest request, CancellationToken ct = default);
    Task<OrderDto> ChangeQuantityAsync(int orderItemId, int delta, CancellationToken ct = default);
    Task<OrderDto?> RemoveItemAsync(int orderItemId, CancellationToken ct = default);
    Task SetItemCommentAsync(int orderItemId, string? comment, CancellationToken ct = default);
    Task SendToKitchenAsync(int orderId, CancellationToken ct = default);
    Task SetItemStatusAsync(int orderItemId, OrderItemStatus status, CancellationToken ct = default);
    Task<List<OrderDto>> GetKitchenOrdersAsync(CancellationToken ct = default);

    /// <summary>Waiter fires the second courses for a table, alerting the kitchen in real time.</summary>
    Task FireSecondCoursesAsync(int orderId, CancellationToken ct = default);

    /// <summary>Kitchen acknowledges the fired second courses, clearing the alert on both screens.</summary>
    Task AcknowledgeSecondCoursesAsync(int orderId, CancellationToken ct = default);
    Task<int> CheckoutAsync(int orderId, PaymentMethod method, CancellationToken ct = default);
    Task<int> CheckoutAsync(int orderId, IReadOnlyList<PaymentInput> payments, CancellationToken ct = default);
}

public class OrderService : IOrderService
{
    private static readonly OrderStatus[] ActiveStatuses =
        { OrderStatus.Open, OrderStatus.Sent, OrderStatus.InPreparation, OrderStatus.Ready, OrderStatus.Delivered };

    private static readonly OrderStatus[] KitchenStatuses =
        { OrderStatus.Sent, OrderStatus.InPreparation, OrderStatus.Ready };

    private readonly IUnitOfWork _uow;
    private readonly IKitchenNotifier _notifier;

    public OrderService(IUnitOfWork uow, IKitchenNotifier notifier)
    {
        _uow = uow;
        _notifier = notifier;
    }

    private IQueryable<Order> FullOrders() => _uow.Repository<Order>().Query()
        .Include(o => o.Table)
        .Include(o => o.Waiter)
        .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category)
        .Include(o => o.Items).ThenInclude(i => i.Extras);

    public async Task<List<OrderDto>> GetOpenOrdersAsync(CancellationToken ct = default)
    {
        var orders = await FullOrders()
            .Where(o => ActiveStatuses.Contains(o.Status))
            .OrderBy(o => o.CreatedAt).ToListAsync(ct);
        return orders.Select(Map).ToList();
    }

    public async Task<OrderDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var order = await FullOrders().FirstOrDefaultAsync(o => o.Id == id, ct);
        return order is null ? null : Map(order);
    }

    public async Task<OrderDto?> GetActiveByTableAsync(int tableId, CancellationToken ct = default)
    {
        var memberIds = await GroupMemberIdsAsync(tableId, ct);
        var order = await FullOrders()
            .Where(o => memberIds.Contains(o.TableId) && ActiveStatuses.Contains(o.Status))
            .OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync(ct);
        return order is null ? null : Map(order);
    }

    public async Task<OrderDto> OpenOrderAsync(int tableId, int waiterId, CancellationToken ct = default)
    {
        var existing = await GetActiveByTableAsync(tableId, ct);
        if (existing is not null) return existing;

        var table = await _uow.Repository<RestaurantTable>().GetByIdAsync(tableId, ct)
            ?? throw new KeyNotFoundException($"Table {tableId} not found.");

        var members = await GroupMembersAsync(table, ct);
        var primaryId = members.Min(m => m.Id);   // the group's order lives on its lowest-Id table

        var order = new Order { TableId = primaryId, WaiterId = waiterId, Status = OrderStatus.Open, Number = "…" };
        await _uow.Repository<Order>().AddAsync(order, ct);
        await _uow.SaveChangesAsync(ct);

        order.Number = $"O-{order.Id:D5}";
        _uow.Repository<Order>().Update(order);
        foreach (var m in members)
        {
            m.Status = TableStatus.Occupied;
            _uow.Repository<RestaurantTable>().Update(m);
        }
        await _uow.SaveChangesAsync(ct);

        return (await GetByIdAsync(order.Id, ct))!;
    }

    private async Task<List<int>> GroupMemberIdsAsync(int tableId, CancellationToken ct)
    {
        var table = await _uow.Repository<RestaurantTable>().GetByIdAsync(tableId, ct);
        return table is null ? new() : (await GroupMembersAsync(table, ct)).Select(t => t.Id).ToList();
    }

    private async Task<List<RestaurantTable>> GroupMembersAsync(RestaurantTable table, CancellationToken ct)
    {
        if (table.GroupId is null) return new() { table };
        return await _uow.Repository<RestaurantTable>().Query()
            .Where(t => t.GroupId == table.GroupId).ToListAsync(ct);
    }

    public async Task<OrderDto> AddItemAsync(AddItemRequest request, CancellationToken ct = default)
    {
        var order = await _uow.Repository<Order>().Query()
            .Include(o => o.Items).ThenInclude(i => i.Extras)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct)
            ?? throw new KeyNotFoundException($"Order {request.OrderId} not found.");

        var product = await _uow.Repository<Product>().GetByIdAsync(request.ProductId, ct)
            ?? throw new KeyNotFoundException($"Product {request.ProductId} not found.");

        var hasExtras = request.ExtraIds is { Count: > 0 };

        // Merge into an existing pending line for the same product + comment, but only when neither
        // the new line nor the existing one carries extras (lines with extras stay distinct).
        var existing = hasExtras ? null : order.Items.FirstOrDefault(i =>
            i.ProductId == product.Id && i.Status == OrderItemStatus.Pending
            && i.Comment == request.Comment && i.Extras.Count == 0);

        if (existing is not null)
        {
            existing.Quantity += request.Quantity;
            _uow.Repository<OrderItem>().Update(existing);
        }
        else
        {
            var item = new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                Quantity = request.Quantity,
                UnitPrice = product.Price,
                VatRate = product.VatRate,
                Comment = request.Comment,
                Status = OrderItemStatus.Pending
            };

            if (hasExtras)
            {
                var extras = await _uow.Repository<Extra>().Query()
                    .Where(e => request.ExtraIds!.Contains(e.Id)).ToListAsync(ct);
                foreach (var ex in extras)
                    item.Extras.Add(new OrderItemExtra { Name = ex.Name, Price = ex.Price, ExtraId = ex.Id });
            }

            await _uow.Repository<OrderItem>().AddAsync(item, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return (await GetByIdAsync(order.Id, ct))!;
    }

    public async Task<OrderDto> ChangeQuantityAsync(int orderItemId, int delta, CancellationToken ct = default)
    {
        var item = await _uow.Repository<OrderItem>().GetByIdAsync(orderItemId, ct)
            ?? throw new KeyNotFoundException($"Order item {orderItemId} not found.");

        item.Quantity += delta;
        if (item.Quantity <= 0)
            _uow.Repository<OrderItem>().Remove(item);
        else
            _uow.Repository<OrderItem>().Update(item);

        await _uow.SaveChangesAsync(ct);
        return (await GetByIdAsync(item.OrderId, ct))!;
    }

    public async Task<OrderDto?> RemoveItemAsync(int orderItemId, CancellationToken ct = default)
    {
        var item = await _uow.Repository<OrderItem>().GetByIdAsync(orderItemId, ct);
        if (item is null) return null;
        var orderId = item.OrderId;
        _uow.Repository<OrderItem>().Remove(item);
        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(orderId, ct);
    }

    public async Task SetItemCommentAsync(int orderItemId, string? comment, CancellationToken ct = default)
    {
        var item = await _uow.Repository<OrderItem>().GetByIdAsync(orderItemId, ct);
        if (item is null) return;
        item.Comment = comment;
        _uow.Repository<OrderItem>().Update(item);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task SendToKitchenAsync(int orderId, CancellationToken ct = default)
    {
        var order = await _uow.Repository<Order>().Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");

        foreach (var item in order.Items.Where(i => i.Status == OrderItemStatus.Pending))
            item.Status = OrderItemStatus.Preparing;

        order.Status = OrderStatus.Sent;
        _uow.Repository<Order>().Update(order);
        await _uow.SaveChangesAsync(ct);

        await _notifier.OrderSentToKitchenAsync(orderId);
    }

    public async Task SetItemStatusAsync(int orderItemId, OrderItemStatus status, CancellationToken ct = default)
    {
        var item = await _uow.Repository<OrderItem>().Query()
            .Include(i => i.Order).ThenInclude(o => o.Items)
            .FirstOrDefaultAsync(i => i.Id == orderItemId, ct)
            ?? throw new KeyNotFoundException($"Order item {orderItemId} not found.");

        item.Status = status;

        var order = item.Order;
        if (order.Items.All(i => i.Status == OrderItemStatus.Ready || i.Status == OrderItemStatus.Delivered)
            && order.Items.Any(i => i.Status == OrderItemStatus.Ready))
        {
            order.Status = OrderStatus.Ready;
        }
        else if (order.Items.Any(i => i.Status == OrderItemStatus.Preparing))
        {
            order.Status = OrderStatus.InPreparation;
        }

        _uow.Repository<OrderItem>().Update(item);
        _uow.Repository<Order>().Update(order);
        await _uow.SaveChangesAsync(ct);

        await _notifier.OrderItemStatusChangedAsync(order.Id, orderItemId);
        if (order.Status == OrderStatus.Ready)
            await _notifier.OrderReadyAsync(order.Id);
    }

    public async Task<List<OrderDto>> GetKitchenOrdersAsync(CancellationToken ct = default)
    {
        var orders = await FullOrders()
            .Where(o => KitchenStatuses.Contains(o.Status))
            .OrderBy(o => o.CreatedAt).ToListAsync(ct);
        // Drinks are served from the bar, so an order made up solely of drinks never reaches the
        // kitchen display. Orders that mix food and drinks are kept; the KDS hides the drink lines.
        return orders.Select(Map).Where(o => o.Items.Any(i => !i.IsDrink)).ToList();
    }

    public async Task FireSecondCoursesAsync(int orderId, CancellationToken ct = default)
    {
        var order = await _uow.Repository<Order>().GetByIdAsync(orderId, ct)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");

        order.SecondsFiredAt = DateTime.UtcNow;
        _uow.Repository<Order>().Update(order);
        await _uow.SaveChangesAsync(ct);

        await _notifier.SecondCoursesFiredAsync(orderId);
    }

    public async Task AcknowledgeSecondCoursesAsync(int orderId, CancellationToken ct = default)
    {
        var order = await _uow.Repository<Order>().GetByIdAsync(orderId, ct)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");

        order.SecondsFiredAt = null;
        _uow.Repository<Order>().Update(order);
        await _uow.SaveChangesAsync(ct);

        await _notifier.SecondCoursesFiredAsync(orderId);
    }

    public async Task<int> CheckoutAsync(int orderId, PaymentMethod method, CancellationToken ct = default)
    {
        var order = await FullOrders().FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");
        return await FinalizeCheckoutAsync(order, new[] { new PaymentInput(order.Total, method) }, ct);
    }

    public async Task<int> CheckoutAsync(int orderId, IReadOnlyList<PaymentInput> payments, CancellationToken ct = default)
    {
        if (payments is null || payments.Count == 0)
            throw new InvalidOperationException("At least one payment is required.");

        var order = await FullOrders().FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");

        var tendered = payments.Sum(p => p.Amount);
        if (tendered + 0.001m < order.Total)
            throw new InvalidOperationException(
                $"Payments ({tendered:0.00}) do not cover the total ({order.Total:0.00}).");

        return await FinalizeCheckoutAsync(order, payments, ct);
    }

    private async Task<int> FinalizeCheckoutAsync(Order order, IReadOnlyList<PaymentInput> payments, CancellationToken ct)
    {
        // A single tender keeps its method; a genuine split is recorded as "Other" on the invoice.
        var distinctMethods = payments.Select(p => p.Method).Distinct().ToList();
        var invoiceMethod = distinctMethods.Count == 1 ? distinctMethods[0] : PaymentMethod.Other;

        var invoice = new Invoice
        {
            OrderId = order.Id,
            Number = "…",
            Subtotal = order.Subtotal,
            VatTotal = order.VatTotal,
            Total = order.Total,
            PaymentMethod = invoiceMethod
        };
        await _uow.Repository<Invoice>().AddAsync(invoice, ct);
        await _uow.SaveChangesAsync(ct);

        invoice.Number = $"INV-{invoice.Id:D6}";
        _uow.Repository<Invoice>().Update(invoice);

        foreach (var p in payments)
            await _uow.Repository<Payment>().AddAsync(
                new Payment { InvoiceId = invoice.Id, Amount = p.Amount, Method = p.Method }, ct);

        order.Status = OrderStatus.Paid;
        order.ClosedAt = DateTime.UtcNow;
        _uow.Repository<Order>().Update(order);

        // Full payment is the only trigger that frees a table (including reserved ones).
        // Joined tables are all freed together.
        var primary = await _uow.Repository<RestaurantTable>().GetByIdAsync(order.TableId, ct);
        if (primary is not null)
        {
            foreach (var table in await GroupMembersAsync(primary, ct))
            {
                var hasUpcomingReservation = await _uow.Repository<Reservation>().Query().AnyAsync(r =>
                    r.TableId == table.Id &&
                    (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Pending) &&
                    r.Date >= DateTime.Today, ct);
                table.Status = hasUpcomingReservation ? TableStatus.Reserved : TableStatus.Available;
                _uow.Repository<RestaurantTable>().Update(table);
            }
        }

        await _uow.SaveChangesAsync(ct);
        return invoice.Id;
    }

    private static OrderDto Map(Order o)
    {
        var items = o.Items
            .OrderBy(i => i.Id)
            .Select(i => new OrderItemDto(
                i.Id, i.ProductId, i.Product?.Name ?? "?", i.Quantity, i.UnitPrice, i.VatRate,
                i.Comment, i.Status,
                i.Extras.Select(e => new OrderItemExtraDto(e.Id, e.Name, e.Price)).ToList(),
                i.LineGross,
                i.Product?.Category?.Kind == CategoryKind.Drink))
            .ToList();

        return new OrderDto(
            o.Id, o.Number, o.Status, o.TableId, o.Table?.Name ?? "?",
            o.WaiterId, o.Waiter?.FullName ?? "?", o.CreatedAt, o.Notes,
            items, o.Subtotal, o.VatTotal, o.Total, o.SecondsFiredAt);
    }
}
