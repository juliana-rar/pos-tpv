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
    /// Set when the waiter "fires the first courses" for this table, signalling the kitchen to
    /// start the starters. Cleared (back to null) once the kitchen acknowledges it.
    /// </summary>
    public DateTime? FirstsFiredAt { get; set; }

    /// <summary>
    /// Set when the waiter "fires the second courses" for this table, signalling the kitchen to
    /// start the mains. Cleared (back to null) once the kitchen acknowledges it.
    /// </summary>
    public DateTime? SecondsFiredAt { get; set; }

    /// <summary>Set the last time all pending drinks of this order were marked as served.</summary>
    public DateTime? DrinksServedAt { get; set; }

    /// <summary>
    /// Set once the first time firsts are fired and never cleared, so the kitchen can tell at a
    /// glance whether firsts already went out even after acknowledging the <see cref="FirstsFiredAt"/> bell.
    /// </summary>
    public DateTime? FirstsSentAt { get; set; }

    /// <summary>Same as <see cref="FirstsSentAt"/> but for second courses.</summary>
    public DateTime? SecondsSentAt { get; set; }

    /// <summary>
    /// Set when the waiter "fires the desserts" for this table, signalling the kitchen to
    /// start them. Cleared (back to null) once the kitchen acknowledges it.
    /// </summary>
    public DateTime? DessertsFiredAt { get; set; }

    /// <summary>Same as <see cref="FirstsSentAt"/> but for desserts.</summary>
    public DateTime? DessertsSentAt { get; set; }

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
