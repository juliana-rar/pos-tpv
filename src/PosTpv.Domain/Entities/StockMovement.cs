using PosTpv.Domain.Common;
using PosTpv.Domain.Enums;

namespace PosTpv.Domain.Entities;

/// <summary>An audit trail entry for a product's stock quantity change — positive for purchases
/// and manual increases, negative for sales and manual decreases.</summary>
public class StockMovement : BaseEntity
{
    public decimal QuantityChange { get; set; }
    public StockMovementReason Reason { get; set; }
    public string? Note { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
}
