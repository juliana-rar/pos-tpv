using PosTpv.Domain.Enums;

namespace PosTpv.Application.DTOs;

public record OrderItemExtraDto(int Id, string Name, decimal Price);

public record OrderItemDto(
    int Id, int ProductId, string ProductName, int Quantity, decimal UnitPrice,
    decimal VatRate, string? Comment, OrderItemStatus Status,
    IReadOnlyList<OrderItemExtraDto> Extras, decimal LineTotal, bool IsDrink);

public record OrderDto(
    int Id, string Number, OrderStatus Status, int TableId, string TableName,
    int WaiterId, string WaiterName, DateTime CreatedAt, string? Notes,
    IReadOnlyList<OrderItemDto> Items, decimal Subtotal, decimal VatTotal, decimal Total,
    DateTime? SecondsFiredAt);

/// <summary>Request to add a product line to an order, optionally with selected extras.</summary>
public record AddItemRequest(int OrderId, int ProductId, int Quantity = 1, string? Comment = null,
    IReadOnlyList<int>? ExtraIds = null);

/// <summary>A single tender against a bill; several make up a split payment.</summary>
public record PaymentInput(decimal Amount, PaymentMethod Method);
