using PosTpv.Domain.Enums;

namespace PosTpv.Application.DTOs;

public record OrderItemExtraDto(int Id, int? ExtraId, string Name, decimal Price);

public record OrderItemDto(
    int Id, int ProductId, string ProductName, int Quantity, decimal UnitPrice,
    decimal VatRate, string? Comment, OrderItemStatus Status,
    IReadOnlyList<OrderItemExtraDto> Extras, decimal LineTotal, bool IsDrink, CourseType Course);

public record OrderDto(
    int Id, string Number, OrderStatus Status, int TableId, string TableName,
    int WaiterId, string WaiterName, DateTime CreatedAt, string? Notes,
    IReadOnlyList<OrderItemDto> Items, decimal Subtotal, decimal VatTotal, decimal Total,
    DateTime? FirstsFiredAt, DateTime? SecondsFiredAt);

/// <summary>Request to add a product line to an order, optionally with selected extras.</summary>
/// <remarks>
/// When <c>Merge</c> is false the line is always created as its own row, even if an identical
/// pending line already exists — used to keep each drink on its own line so it can carry its own comment.
/// </remarks>
public record AddItemRequest(int OrderId, int ProductId, int Quantity = 1, string? Comment = null,
    IReadOnlyList<int>? ExtraIds = null, bool Merge = true);

/// <summary>A single tender against a bill; several make up a split payment.</summary>
public record PaymentInput(decimal Amount, PaymentMethod Method);
