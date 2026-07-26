using PosTpv.Domain.Common;
using PosTpv.Domain.Enums;

namespace PosTpv.Domain.Entities;

/// <summary>A single line of an order: a product, a quantity, optional comment and extras.</summary>
public class OrderItem : BaseEntity
{
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }   // price captured at time of sale
    public decimal VatRate { get; set; }      // percentage captured at time of sale
    public string? Comment { get; set; }
    public OrderItemStatus Status { get; set; } = OrderItemStatus.Pending;

    /// <summary>True when this line was added as a comp/invitation (zero price by choice, not because
    /// the product's catalog price happens to be zero).</summary>
    public bool IsInvited { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public ICollection<OrderItemExtra> Extras { get; set; } = new List<OrderItemExtra>();

    public decimal ExtrasUnit => Extras.Sum(e => e.Price);
    public decimal LineGross => (UnitPrice + ExtrasUnit) * Quantity;
    public decimal LineNet => LineGross / (1 + VatRate / 100m);
    public decimal LineVat => LineGross - LineNet;
}
