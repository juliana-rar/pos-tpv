using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>A single product/quantity/cost line of a <see cref="Purchase"/>.</summary>
public class PurchaseLine : BaseEntity
{
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }

    public int PurchaseId { get; set; }
    public Purchase Purchase { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal LineTotal => Quantity * UnitCost;
}
