using FluentValidation;
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

    /// <summary>Moves an open order to a different (currently free) table — frees the old table
    /// (back to Reserved if an upcoming reservation still needs it, else Available) and occupies the new one.</summary>
    Task<OrderDto> MoveOrderToTableAsync(int orderId, int newTableId, CancellationToken ct = default);
    Task<OrderDto> AddItemAsync(AddItemRequest request, CancellationToken ct = default);
    Task<OrderDto> ChangeQuantityAsync(int orderItemId, int delta, CancellationToken ct = default);
    Task<OrderDto?> RemoveItemAsync(int orderItemId, CancellationToken ct = default);
    Task SetItemCommentAsync(int orderItemId, string? comment, CancellationToken ct = default);
    Task<OrderDto?> SetItemExtrasAsync(int orderItemId, IReadOnlyList<int> extraIds, CancellationToken ct = default);

    /// <summary>Marks an already-added line as invited (zero price, not charged) or reverts it back
    /// to the product's current catalog price.</summary>
    Task<OrderDto?> SetItemInvitedAsync(int orderItemId, bool invited, CancellationToken ct = default);

    /// <summary>
    /// Steps each line's effective course (Starter/Main/Dessert) up (delta -1) or down (delta +1)
    /// by one — the order editor's Up/Down toolbar buttons while Select is armed, moving the
    /// selected lines from e.g. first course to second course. Drink lines are left untouched
    /// (they stay grouped under Drinks regardless of course). Clamped at either end: pushing a
    /// dessert line further down, or a starter line further up, is a no-op rather than wrapping.
    /// </summary>
    Task<OrderDto> MoveItemsCourseAsync(int orderId, IReadOnlyList<int> orderItemIds, int delta, CancellationToken ct = default);
    Task SendToKitchenAsync(int orderId, CancellationToken ct = default);
    Task SetItemStatusAsync(int orderItemId, OrderItemStatus status, CancellationToken ct = default);

    /// <summary>
    /// Marks every still-pending drink line of an order as served in a single round-trip and
    /// broadcasts one real-time update, so the waiter serves the whole drinks block at once.
    /// </summary>
    Task ServeDrinksAsync(int orderId, CancellationToken ct = default);

    /// <summary>Undoes <see cref="ServeDrinksAsync"/>: puts every served drink line back to pending.</summary>
    Task UnserveDrinksAsync(int orderId, CancellationToken ct = default);

    /// <summary>
    /// Marks every ready-to-serve first-course line of an order as delivered in a single
    /// round-trip, mirroring <see cref="ServeDrinksAsync"/> for the first-course block.
    /// </summary>
    Task ServeFirstCoursesAsync(int orderId, CancellationToken ct = default);

    /// <summary>Undoes <see cref="ServeFirstCoursesAsync"/>: puts every served first-course line back to ready.</summary>
    Task UnserveFirstCoursesAsync(int orderId, CancellationToken ct = default);

    /// <summary>Marks every ready-to-serve second-course line of an order as delivered, mirroring <see cref="ServeFirstCoursesAsync"/>.</summary>
    Task ServeSecondCoursesAsync(int orderId, CancellationToken ct = default);

    /// <summary>Undoes <see cref="ServeSecondCoursesAsync"/>: puts every served second-course line back to ready.</summary>
    Task UnserveSecondCoursesAsync(int orderId, CancellationToken ct = default);

    /// <summary>Marks every ready-to-serve dessert line of an order as delivered, mirroring <see cref="ServeFirstCoursesAsync"/>.</summary>
    Task ServeDessertCoursesAsync(int orderId, CancellationToken ct = default);

    /// <summary>Undoes <see cref="ServeDessertCoursesAsync"/>: puts every served dessert line back to ready.</summary>
    Task UnserveDessertCoursesAsync(int orderId, CancellationToken ct = default);

    /// <summary>
    /// Marks every preparing food line (any course, drinks excluded) of an order as ready in a
    /// single round-trip, backing the kitchen display's "Mark all ready" button.
    /// </summary>
    Task MarkFoodReadyAsync(int orderId, CancellationToken ct = default);

    /// <summary>Undoes <see cref="MarkFoodReadyAsync"/>: puts every ready (not yet delivered) food line back to preparing.</summary>
    Task UnmarkFoodReadyAsync(int orderId, CancellationToken ct = default);

    Task<List<OrderDto>> GetKitchenOrdersAsync(CancellationToken ct = default);

    /// <summary>Waiter fires the second courses for a table, alerting the kitchen in real time.</summary>
    Task FireSecondCoursesAsync(int orderId, CancellationToken ct = default);

    /// <summary>Kitchen acknowledges the fired second courses, clearing the alert on both screens.</summary>
    Task AcknowledgeSecondCoursesAsync(int orderId, CancellationToken ct = default);

    /// <summary>Reverts a mistaken "second courses fired": clears both the alert and the persistent fired marker.</summary>
    Task UndoSecondCoursesFiredAsync(int orderId, CancellationToken ct = default);
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
    private readonly IValidator<AddItemRequest> _addItemValidator;

    public OrderService(IUnitOfWork uow, IKitchenNotifier notifier, IValidator<AddItemRequest> addItemValidator)
    {
        _uow = uow;
        _notifier = notifier;
        _addItemValidator = addItemValidator;
    }

    private IQueryable<Order> FullOrders() => _uow.Repository<Order>().Query()
        .Include(o => o.Table)
        .Include(o => o.Waiter)
        .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category)
        .Include(o => o.Items).ThenInclude(i => i.Extras)
        .AsSplitQuery();

    /// <summary>Same shape as <see cref="FullOrders"/> but untracked — for endpoints that only ever return a DTO.</summary>
    private IQueryable<Order> FullOrdersReadOnly() => _uow.Repository<Order>().QueryNoTracking()
        .Include(o => o.Table)
        .Include(o => o.Waiter)
        .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category)
        .Include(o => o.Items).ThenInclude(i => i.Extras)
        .AsSplitQuery();

    /// <summary>Loads a tracked order with its items' products/categories, for the serve/unserve/status mutation paths.</summary>
    private async Task<Order> LoadOrderWithCategoriesAsync(int orderId, CancellationToken ct) =>
        await _uow.Repository<Order>().Query()
            .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
        ?? throw new KeyNotFoundException($"Order {orderId} not found.");

    /// <summary>Re-reads an order just saved in this same call, for building the response DTO.</summary>
    private async Task<OrderDto> GetByIdOrThrowAsync(int id, CancellationToken ct) =>
        await GetByIdAsync(id, ct) ?? throw new InvalidOperationException($"Order {id} was removed while it was being updated.");

    public async Task<List<OrderDto>> GetOpenOrdersAsync(CancellationToken ct = default)
    {
        var orders = await FullOrdersReadOnly()
            .Where(o => ActiveStatuses.Contains(o.Status))
            .OrderBy(o => o.CreatedAt).ToListAsync(ct);
        return orders.Select(Map).ToList();
    }

    public async Task<OrderDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var order = await FullOrdersReadOnly().FirstOrDefaultAsync(o => o.Id == id, ct);
        return order is null ? null : Map(order);
    }

    public async Task<OrderDto?> GetActiveByTableAsync(int tableId, CancellationToken ct = default)
    {
        var memberIds = await GroupMemberIdsAsync(tableId, ct);
        var order = await FullOrdersReadOnly()
            .Where(o => memberIds.Contains(o.TableId) && ActiveStatuses.Contains(o.Status))
            .OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync(ct);
        return order is null ? null : Map(order);
    }

    public Task<OrderDto> OpenOrderAsync(int tableId, int waiterId, CancellationToken ct = default) =>
        // The "is there already an active order for this table?" check and the order creation
        // that follows are two round-trips; without a transaction, two waiters opening the same
        // table at the same instant can both pass the check and create duplicate orders.
        _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            var existing = await GetActiveByTableAsync(tableId, innerCt);
            if (existing is not null) return existing;

            var table = await _uow.Repository<RestaurantTable>().GetByIdAsync(tableId, innerCt)
                ?? throw new KeyNotFoundException($"Table {tableId} not found.");

            var members = await GroupMembersAsync(table, innerCt);
            var primaryId = members.Min(m => m.Id);   // the group's order lives on its lowest-Id table

            var order = new Order { TableId = primaryId, WaiterId = waiterId, Status = OrderStatus.Open, Number = "…" };
            await _uow.Repository<Order>().AddAsync(order, innerCt);
            await _uow.SaveChangesAsync(innerCt);

            order.Number = $"O-{order.Id:D5}";
            _uow.Repository<Order>().Update(order);
            foreach (var m in members)
            {
                m.Status = TableStatus.Occupied;
                _uow.Repository<RestaurantTable>().Update(m);
            }
            await _uow.SaveChangesAsync(innerCt);

            return await GetByIdOrThrowAsync(order.Id, innerCt);
        }, ct);

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

    public Task<OrderDto> MoveOrderToTableAsync(int orderId, int newTableId, CancellationToken ct = default) =>
        _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            var order = await _uow.Repository<Order>().GetByIdAsync(orderId, innerCt)
                ?? throw new KeyNotFoundException($"Order {orderId} not found.");

            var oldTable = await _uow.Repository<RestaurantTable>().GetByIdAsync(order.TableId, innerCt)
                ?? throw new KeyNotFoundException($"Table {order.TableId} not found.");
            var newTable = await _uow.Repository<RestaurantTable>().GetByIdAsync(newTableId, innerCt)
                ?? throw new KeyNotFoundException($"Table {newTableId} not found.");

            // A table held for an imminent reservation has no active order yet, so the check below
            // wouldn't catch it: block reassigning onto it, same as the floor-plan lock rule.
            if (newTable.Status == TableStatus.Reserved)
                throw new InvalidOperationException($"Table {newTableId} is reserved and cannot be reassigned.");

            if (await GetActiveByTableAsync(newTableId, innerCt) is not null)
                throw new InvalidOperationException($"Table {newTableId} already has an active order.");

            order.TableId = newTableId;
            _uow.Repository<Order>().Update(order);

            // Same freeing rule FinalizeCheckoutAsync uses for the vacated table: back to Reserved
            // if an upcoming reservation still needs it, otherwise Available.
            await FreeTablesAsync(await GroupMembersAsync(oldTable, innerCt), innerCt);

            newTable.Status = TableStatus.Occupied;
            _uow.Repository<RestaurantTable>().Update(newTable);

            await _uow.SaveChangesAsync(innerCt);
            return await GetByIdOrThrowAsync(order.Id, innerCt);
        }, ct);

    /// <summary>
    /// Frees a set of tables back to Reserved (if an upcoming reservation still needs one of them)
    /// or Available, in a single batched query instead of one reservation lookup per table.
    /// </summary>
    private async Task FreeTablesAsync(IReadOnlyList<RestaurantTable> tables, CancellationToken ct)
    {
        var tableIds = tables.Select(t => t.Id).ToList();
        var reservedTableIds = await _uow.Repository<Reservation>().QueryNoTracking()
            .Where(r => (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Pending)
                        && r.Date >= DateTime.Today && r.Tables.Any(t => tableIds.Contains(t.Id)))
            .SelectMany(r => r.Tables.Select(t => t.Id))
            .Where(id => tableIds.Contains(id))
            .Distinct()
            .ToListAsync(ct);

        foreach (var table in tables)
        {
            table.Status = reservedTableIds.Contains(table.Id) ? TableStatus.Reserved : TableStatus.Available;
            _uow.Repository<RestaurantTable>().Update(table);
        }
    }

    public async Task<OrderDto> AddItemAsync(AddItemRequest request, CancellationToken ct = default)
    {
        await _addItemValidator.ValidateAndThrowAsync(request, ct);

        var order = await _uow.Repository<Order>().Query()
            .Include(o => o.Items).ThenInclude(i => i.Extras)
            .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct)
            ?? throw new KeyNotFoundException($"Order {request.OrderId} not found.");

        var product = await _uow.Repository<Product>().QueryNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, ct)
            ?? throw new KeyNotFoundException($"Product {request.ProductId} not found.");

        var hasExtras = request.ExtraIds is { Count: > 0 };

        // Merge into an existing pending line for the same product + comment, but only when neither
        // the new line nor the existing one carries extras (lines with extras stay distinct), and only
        // when the caller allows merging (drinks are added unmerged so each keeps its own comment).
        var existing = (hasExtras || !request.Merge || request.Invited) ? null : order.Items.FirstOrDefault(i =>
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
                UnitPrice = request.Invited ? 0m : product.Price,
                VatRate = product.VatRate,
                Comment = request.Comment,
                Status = OrderItemStatus.Pending,
                IsInvited = request.Invited
            };

            if (hasExtras)
            {
                var extras = await _uow.Repository<Extra>().QueryNoTracking()
                    .Where(e => request.ExtraIds!.Contains(e.Id)).ToListAsync(ct);
                foreach (var ex in extras)
                    item.Extras.Add(new OrderItemExtra { Name = ex.Name, Price = ex.Price, ExtraId = ex.Id });
            }

            await _uow.Repository<OrderItem>().AddAsync(item, ct);
        }

        // A newly added line is always Pending, i.e. not yet sent to the kitchen. If the order
        // had already progressed past Open (Sent/InPreparation/Ready/Delivered), flip it back so
        // the waiter sees it needs another trip to the kitchen instead of a stale "Sent" badge.
        if (order.Status != OrderStatus.Open)
        {
            order.Status = OrderStatus.Open;
            _uow.Repository<Order>().Update(order);
        }

        // Ordering dessert means the mains are done, so auto-serve any that aren't marked
        // delivered yet — same green "served" treatment as drinks/firsts, without a separate click.
        var pendingSeconds = product.Category?.Course == CourseType.Dessert && product.Category?.Kind != CategoryKind.Drink
            ? order.Items
                .Where(i => IsCourse(i, CourseType.Main) && i.Status != OrderItemStatus.Delivered)
                .ToList()
            : new List<OrderItem>();
        foreach (var item in pendingSeconds)
        {
            item.Status = OrderItemStatus.Delivered;
            _uow.Repository<OrderItem>().Update(item);
        }

        await _uow.SaveChangesAsync(ct);
        if (pendingSeconds.Count > 0)
            await _notifier.SecondCoursesServedAsync(order.Id);
        return await GetByIdOrThrowAsync(order.Id, ct);
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

        // A quantity bump isn't a re-serve: skip the UpdatedAt auto-stamp so an already-served
        // line doesn't fall out of its green "served together" batch (see GroupByServeBatch,
        // keyed on UpdatedAt) just because someone tapped +.
        _uow.SkipAuditStamp = true;
        try { await _uow.SaveChangesAsync(ct); }
        finally { _uow.SkipAuditStamp = false; }

        return await GetByIdOrThrowAsync(item.OrderId, ct);
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

        // Same reasoning as ChangeQuantityAsync: editing a comment isn't a re-serve, so it must
        // not disturb UpdatedAt and knock an already-served line out of its green batch.
        _uow.SkipAuditStamp = true;
        try { await _uow.SaveChangesAsync(ct); }
        finally { _uow.SkipAuditStamp = false; }
    }

    public async Task<OrderDto?> SetItemExtrasAsync(int orderItemId, IReadOnlyList<int> extraIds, CancellationToken ct = default)
    {
        var item = await _uow.Repository<OrderItem>().Query()
            .Include(i => i.Extras)
            .FirstOrDefaultAsync(i => i.Id == orderItemId, ct);
        if (item is null) return null;

        foreach (var old in item.Extras.ToList())
            _uow.Repository<OrderItemExtra>().Remove(old);
        item.Extras.Clear();

        var extras = await _uow.Repository<Extra>().QueryNoTracking()
            .Where(e => extraIds.Contains(e.Id)).ToListAsync(ct);
        foreach (var ex in extras)
            item.Extras.Add(new OrderItemExtra { Name = ex.Name, Price = ex.Price, ExtraId = ex.Id });

        _uow.Repository<OrderItem>().Update(item);

        // Same reasoning as ChangeQuantityAsync: picking extras on an existing line isn't a
        // re-serve, so it must not disturb UpdatedAt and knock an already-served line out of its
        // green batch (see GroupByServeBatch) — this is exactly what put "CAFE CON LECHE + Extra
        // sauce" in its own separate div after the rest of its 23:08 round was already served.
        _uow.SkipAuditStamp = true;
        try { await _uow.SaveChangesAsync(ct); }
        finally { _uow.SkipAuditStamp = false; }

        return await GetByIdAsync(item.OrderId, ct);
    }

    public async Task<OrderDto?> SetItemInvitedAsync(int orderItemId, bool invited, CancellationToken ct = default)
    {
        var item = await _uow.Repository<OrderItem>().Query()
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.Id == orderItemId, ct);
        if (item is null) return null;

        item.IsInvited = invited;
        item.UnitPrice = invited ? 0m : item.Product.Price;
        _uow.Repository<OrderItem>().Update(item);
        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(item.OrderId, ct);
    }

    public async Task<OrderDto> MoveItemsCourseAsync(int orderId, IReadOnlyList<int> orderItemIds, int delta, CancellationToken ct = default)
    {
        if (orderItemIds.Count == 0) return await GetByIdOrThrowAsync(orderId, ct);

        var items = await _uow.Repository<OrderItem>().Query()
            .Include(i => i.Product).ThenInclude(p => p.Category)
            .Where(i => orderItemIds.Contains(i.Id))
            .ToListAsync(ct);

        // One shared timestamp for the whole batch, so lines moved together land in the same time
        // group at the bottom of their new section instead of scattering across separate ones.
        var movedAt = DateTime.UtcNow;
        foreach (var item in items)
        {
            // Unified 4-step ladder across every section: 0=Drinks, 1=Starter, 2=Main, 3=Dessert.
            // Lets a line step past either end of the food courses into/out of Drinks — moving a
            // dish there treats it as a real drink everywhere (see IsDrink/IsFood above), not just
            // a display change, so only land here if that's genuinely intended.
            var isDrink = IsDrink(item);
            var course = item.CourseOverride ?? item.Product?.Category?.Course ?? CourseType.Main;
            var index = isDrink ? 0 : 1 + (int)course;
            var next = Math.Clamp(index + delta, 0, 3);
            if (next == index) continue;   // already at that end — nothing actually moved

            if (next == 0)
            {
                item.IsDrinkOverride = true;
                item.CourseOverride = null;
            }
            else
            {
                item.IsDrinkOverride = false;
                item.CourseOverride = (CourseType)(next - 1);
            }
            // Bumped so the line sorts last within its new section (see the ordering comment on
            // Map()) instead of staying wherever its original order time placed it — otherwise a
            // line moved to a section with later-ordered items would land above them, buried.
            item.CreatedAt = movedAt;
            _uow.Repository<OrderItem>().Update(item);
        }

        await _uow.SaveChangesAsync(ct);
        return await GetByIdOrThrowAsync(orderId, ct);
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
        ApplyDerivedStatus(order);

        _uow.Repository<OrderItem>().Update(item);
        _uow.Repository<Order>().Update(order);
        await _uow.SaveChangesAsync(ct);

        await _notifier.OrderItemStatusChangedAsync(order.Id, orderItemId);
        if (order.Status == OrderStatus.Ready)
            await _notifier.OrderReadyAsync(order.Id);
    }

    /// <summary>
    /// Loads the order, applies <paramref name="newStatus"/> to every line matching
    /// <paramref name="predicate"/>, recomputes the order's rolled-up status and saves — the shared
    /// shape behind every Serve*/Unserve*/Mark*Ready endpoint below (single DB round-trip and single
    /// real-time notification per call, no per-line queries or events).
    /// </summary>
    private async Task<(Order Order, List<OrderItem> Matched)> SetCourseStatusAsync(
        int orderId, Func<OrderItem, bool> predicate, OrderItemStatus newStatus,
        Action<Order>? beforeSave, CancellationToken ct)
    {
        var order = await LoadOrderWithCategoriesAsync(orderId, ct);
        var matched = order.Items.Where(predicate).ToList();
        if (matched.Count == 0) return (order, matched);

        foreach (var item in matched)
        {
            item.Status = newStatus;
            _uow.Repository<OrderItem>().Update(item);
        }

        beforeSave?.Invoke(order);
        ApplyDerivedStatus(order);
        _uow.Repository<Order>().Update(order);
        await _uow.SaveChangesAsync(ct);
        return (order, matched);
    }

    // Respects a manual move to/out of Drinks (OrderEditor's Up/Down while Select is armed, see
    // MoveItemsCourseAsync) before falling back to the product's own menu category — a line moved
    // into Drinks is treated as a real drink everywhere: served via "Serve drinks", dropped from
    // the kitchen display (both read this same OrderItemDto.IsDrink, set from here in Map).
    private static bool IsDrink(OrderItem i) => i.IsDrinkOverride ?? i.Product?.Category?.Kind == CategoryKind.Drink;
    private static bool IsFood(OrderItem i) => !IsDrink(i);
    // Respects a manual course move (OrderEditor's Up/Down while Select is armed, see
    // MoveItemsCourseAsync) before falling back to the product's own menu category, so a line
    // reassigned e.g. from Starter to Main is served by "Serve seconds" like any other main —
    // not still tied to whichever course its catalog entry happens to sit under.
    private static bool IsCourse(OrderItem i, CourseType course) =>
        IsFood(i) && (i.CourseOverride ?? i.Product?.Category?.Course) == course;

    public async Task ServeDrinksAsync(int orderId, CancellationToken ct = default)
    {
        var (order, matched) = await SetCourseStatusAsync(orderId,
            i => IsDrink(i) && i.Status != OrderItemStatus.Delivered,
            OrderItemStatus.Delivered, o => o.DrinksServedAt = DateTime.UtcNow, ct);
        if (matched.Count == 0) return;

        await _notifier.DrinksServedAsync(order.Id);
        if (order.Status == OrderStatus.Ready)
            await _notifier.OrderReadyAsync(order.Id);
    }

    public async Task UnserveDrinksAsync(int orderId, CancellationToken ct = default)
    {
        var (order, matched) = await SetCourseStatusAsync(orderId,
            i => IsDrink(i) && i.Status == OrderItemStatus.Delivered,
            OrderItemStatus.Pending, o => o.DrinksServedAt = null, ct);
        if (matched.Count == 0) return;

        await _notifier.DrinksServedAsync(order.Id);
    }

    public async Task ServeFirstCoursesAsync(int orderId, CancellationToken ct = default)
    {
        var (order, matched) = await SetCourseStatusAsync(orderId,
            i => IsCourse(i, CourseType.Starter) && i.Status == OrderItemStatus.Ready,
            OrderItemStatus.Delivered, null, ct);
        if (matched.Count == 0) return;

        await _notifier.FirstCoursesServedAsync(order.Id);
        if (order.Status == OrderStatus.Ready)
            await _notifier.OrderReadyAsync(order.Id);
    }

    public async Task UnserveFirstCoursesAsync(int orderId, CancellationToken ct = default)
    {
        var (order, matched) = await SetCourseStatusAsync(orderId,
            i => IsCourse(i, CourseType.Starter) && i.Status == OrderItemStatus.Delivered,
            OrderItemStatus.Ready, null, ct);
        if (matched.Count == 0) return;

        await _notifier.FirstCoursesServedAsync(order.Id);
    }

    // Serving seconds implies the firsts are long gone from the table, so any starter still
    // waiting to be marked served rides along in the same click instead of leaving a stale
    // "Serve firsts" button behind.
    public async Task ServeSecondCoursesAsync(int orderId, CancellationToken ct = default)
    {
        var (order, matched) = await SetCourseStatusAsync(orderId,
            i => i.Status == OrderItemStatus.Ready && (IsCourse(i, CourseType.Main) || IsCourse(i, CourseType.Starter)),
            OrderItemStatus.Delivered, null, ct);
        if (matched.Count == 0) return;

        if (matched.Any(i => IsCourse(i, CourseType.Starter)))
            await _notifier.FirstCoursesServedAsync(order.Id);
        await _notifier.SecondCoursesServedAsync(order.Id);
        if (order.Status == OrderStatus.Ready)
            await _notifier.OrderReadyAsync(order.Id);
    }

    public async Task UnserveSecondCoursesAsync(int orderId, CancellationToken ct = default)
    {
        var (order, matched) = await SetCourseStatusAsync(orderId,
            i => IsCourse(i, CourseType.Main) && i.Status == OrderItemStatus.Delivered,
            OrderItemStatus.Ready, null, ct);
        if (matched.Count == 0) return;

        await _notifier.SecondCoursesServedAsync(order.Id);
    }

    public async Task ServeDessertCoursesAsync(int orderId, CancellationToken ct = default)
    {
        var (order, matched) = await SetCourseStatusAsync(orderId,
            i => IsCourse(i, CourseType.Dessert) && i.Status == OrderItemStatus.Ready,
            OrderItemStatus.Delivered, null, ct);
        if (matched.Count == 0) return;

        await _notifier.DessertCoursesServedAsync(order.Id);
        if (order.Status == OrderStatus.Ready)
            await _notifier.OrderReadyAsync(order.Id);
    }

    public async Task UnserveDessertCoursesAsync(int orderId, CancellationToken ct = default)
    {
        var (order, matched) = await SetCourseStatusAsync(orderId,
            i => IsCourse(i, CourseType.Dessert) && i.Status == OrderItemStatus.Delivered,
            OrderItemStatus.Ready, null, ct);
        if (matched.Count == 0) return;

        await _notifier.DessertCoursesServedAsync(order.Id);
    }

    public async Task MarkFoodReadyAsync(int orderId, CancellationToken ct = default)
    {
        var (order, matched) = await SetCourseStatusAsync(orderId,
            i => IsFood(i) && i.Status == OrderItemStatus.Preparing,
            OrderItemStatus.Ready, null, ct);
        if (matched.Count == 0) return;

        await _notifier.OrderItemStatusChangedAsync(order.Id, matched[0].Id);
        if (order.Status == OrderStatus.Ready)
            await _notifier.OrderReadyAsync(order.Id);
    }

    public async Task UnmarkFoodReadyAsync(int orderId, CancellationToken ct = default)
    {
        var (order, matched) = await SetCourseStatusAsync(orderId,
            i => IsFood(i) && i.Status == OrderItemStatus.Ready,
            OrderItemStatus.Preparing, null, ct);
        if (matched.Count == 0) return;

        await _notifier.OrderItemStatusChangedAsync(order.Id, matched[0].Id);
    }

    /// <summary>Recomputes an order's rolled-up status from its lines (shared by the serve paths).</summary>
    private static void ApplyDerivedStatus(Order order)
    {
        if (order.Items.Any(i => i.Status == OrderItemStatus.Preparing))
        {
            order.Status = OrderStatus.InPreparation;
        }
        else if (order.Items.All(i => i.Status == OrderItemStatus.Delivered))
        {
            order.Status = OrderStatus.Delivered;
        }
        else if (order.Items.Any(i => i.Status == OrderItemStatus.Ready))
        {
            order.Status = OrderStatus.Ready;
        }
        else
        {
            order.Status = OrderStatus.Sent;
        }
    }

    public async Task<List<OrderDto>> GetKitchenOrdersAsync(CancellationToken ct = default)
    {
        var orders = await FullOrdersReadOnly()
            .Where(o => KitchenStatuses.Contains(o.Status))
            .OrderBy(o => o.CreatedAt).ToListAsync(ct);
        // Drinks are served from the bar, so an order made up solely of drinks never reaches the
        // kitchen display. Orders that mix food and drinks are kept; the KDS hides the drink lines.
        return orders.Select(Map).Where(o => o.Items.Any(i => !i.IsDrink)).ToList();
    }

    public async Task FireSecondCoursesAsync(int orderId, CancellationToken ct = default)
    {
        var order = await LoadOrderWithCategoriesAsync(orderId, ct);

        order.SecondsFiredAt = DateTime.UtcNow;
        order.SecondsSentAt ??= order.SecondsFiredAt;

        // Firing the seconds means the starters are done, so auto-serve any that aren't marked
        // delivered yet — same green "served" treatment as drinks, without a separate click.
        var pendingFirsts = order.Items
            .Where(i => IsCourse(i, CourseType.Starter) && i.Status != OrderItemStatus.Delivered)
            .ToList();
        foreach (var item in pendingFirsts)
        {
            item.Status = OrderItemStatus.Delivered;
            _uow.Repository<OrderItem>().Update(item);
        }

        // "Seconds prepared" is the kitchen's word that the mains are done, so mark them Ready
        // right away — otherwise the waiter's "Serve seconds" button wouldn't appear until
        // someone also ticked every main line (or "Mark all ready") separately in /kitchen.
        var pendingMains = order.Items
            .Where(i => IsCourse(i, CourseType.Main) && i.Status == OrderItemStatus.Preparing)
            .ToList();
        foreach (var item in pendingMains)
        {
            item.Status = OrderItemStatus.Ready;
            _uow.Repository<OrderItem>().Update(item);
        }

        ApplyDerivedStatus(order);
        _uow.Repository<Order>().Update(order);
        await _uow.SaveChangesAsync(ct);

        await _notifier.SecondCoursesFiredAsync(orderId);
        if (pendingFirsts.Count > 0)
            await _notifier.FirstCoursesServedAsync(orderId);
        if (pendingMains.Count > 0)
            await _notifier.OrderItemStatusChangedAsync(orderId, pendingMains[0].Id);
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

    public async Task UndoSecondCoursesFiredAsync(int orderId, CancellationToken ct = default)
    {
        var order = await _uow.Repository<Order>().GetByIdAsync(orderId, ct)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");

        order.SecondsFiredAt = null;
        order.SecondsSentAt = null;
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
            await FreeTablesAsync(await GroupMembersAsync(primary, ct), ct);

        await _uow.SaveChangesAsync(ct);
        return invoice.Id;
    }

    private static OrderDto Map(Order o)
    {
        // CreatedAt (not Id) is the display order: normally the two move in lockstep since both
        // are stamped at insert time, but MoveItemsCourseAsync bumps CreatedAt on a moved line so
        // it lands last within its new course section instead of wherever it originally sat.
        var items = o.Items
            .OrderBy(i => i.CreatedAt).ThenBy(i => i.Id)
            .Select(i => new OrderItemDto(
                i.Id, i.ProductId, i.Product?.Name ?? "?", i.Quantity, i.UnitPrice, i.VatRate,
                i.Comment, i.Status,
                i.Extras.Select(e => new OrderItemExtraDto(e.Id, e.ExtraId, e.Name, e.Price)).ToList(),
                i.LineGross,
                IsDrink(i),
                i.CourseOverride ?? i.Product?.Category?.Course ?? CourseType.Main,
                i.CreatedAt, i.Product?.CategoryId ?? 0, i.UpdatedAt, i.IsInvited))
            .ToList();

        return new OrderDto(
            o.Id, o.Number, o.Status, o.TableId, o.Table?.Name ?? "?",
            o.WaiterId, o.Waiter?.FullName ?? "?", o.CreatedAt, o.Notes,
            items, o.Subtotal, o.VatTotal, o.Total, o.SecondsFiredAt, o.DrinksServedAt,
            o.SecondsSentAt, o.Table?.Zone);
    }
}
