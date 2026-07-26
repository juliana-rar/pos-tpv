using PosTpv.Domain.Enums;

namespace PosTpv.Application.DTOs;

public record OrderItemExtraDto(int Id, int? ExtraId, string Name, decimal Price);

public record OrderItemDto(
    int Id, int ProductId, string ProductName, int Quantity, decimal UnitPrice,
    decimal VatRate, string? Comment, OrderItemStatus Status,
    IReadOnlyList<OrderItemExtraDto> Extras, decimal LineTotal, bool IsDrink, CourseType Course,
    DateTime CreatedAt, int CategoryId, DateTime? UpdatedAt, bool IsInvited);

public record OrderDto(
    int Id, string Number, OrderStatus Status, int TableId, string TableName,
    int WaiterId, string WaiterName, DateTime CreatedAt, string? Notes,
    IReadOnlyList<OrderItemDto> Items, decimal Subtotal, decimal VatTotal, decimal Total,
    DateTime? SecondsFiredAt, DateTime? DrinksServedAt,
    DateTime? SecondsSentAt, string? Zone);

/// <summary>Request to add a product line to an order, optionally with selected extras.</summary>
/// <remarks>
/// When <c>Merge</c> is false the line is always created as its own row, even if an identical
/// pending line already exists — used to keep each drink on its own line so it can carry its own comment.
/// When <c>Invited</c> is true the line is captured at zero price (a comp/invitation) and never
/// merges into another line, so a free round never silently zeroes out a paid one.
/// </remarks>
public record AddItemRequest(int OrderId, int ProductId, int Quantity = 1, string? Comment = null,
    IReadOnlyList<int>? ExtraIds = null, bool Merge = true, bool Invited = false);

/// <summary>A single tender against a bill; several make up a split payment.</summary>
public record PaymentInput(decimal Amount, PaymentMethod Method);
