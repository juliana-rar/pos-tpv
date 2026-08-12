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

    /// <summary>Manually reassigns this line to a different course than its product's menu category
    /// (e.g. moved from Starter to Main from the order editor's Up/Down toolbar buttons). Null means
    /// "use the product's own category course" — the default for every line until someone moves it.
    /// Ignored while <see cref="IsDrinkOverride"/> resolves to true, since a drink line has no
    /// course of its own.</summary>
    public CourseType? CourseOverride { get; set; }

    /// <summary>Manually reassigns this line's kitchen routing — moved to (true) or out of (false)
    /// the Drinks group from the order editor's Up/Down toolbar buttons, past the Starter end of
    /// <see cref="CourseOverride"/>'s range. Null means "use the product's own category kind", the
    /// default for every line until someone moves it. A line forced into Drinks is treated exactly
    /// like a real drink everywhere — served via "Serve drinks" instead of a kitchen course, and
    /// dropped from the kitchen display (see OrderService.Map/IsFood) — so only move something here
    /// that genuinely doesn't need cooking.</summary>
    public bool? IsDrinkOverride { get; set; }

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
