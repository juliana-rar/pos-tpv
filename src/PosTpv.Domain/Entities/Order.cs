using PosTpv.Domain.Common;
using PosTpv.Domain.Enums;

namespace PosTpv.Domain.Entities;

/// <summary>An open bill (comanda) attached to a table and a waiter.</summary>
public class Order : BaseEntity
{
    public string Number { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Open;
    public string? Notes { get; set; }
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Set when the waiter "fires the second courses" for this table, signalling the kitchen to
    /// start the mains. Cleared (back to null) once the kitchen acknowledges it.
    /// </summary>
    public DateTime? SecondsFiredAt { get; set; }

    public int TableId { get; set; }
    public RestaurantTable Table { get; set; } = null!;

    public int WaiterId { get; set; }
    public User Waiter { get; set; } = null!;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public Invoice? Invoice { get; set; }

    // Money helpers computed from the current lines.
    public decimal Subtotal => Items.Sum(i => i.LineNet);
    public decimal VatTotal => Items.Sum(i => i.LineVat);
    public decimal Total => Items.Sum(i => i.LineGross);
}
