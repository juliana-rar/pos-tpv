using PosTpv.Domain.Common;
using PosTpv.Domain.Enums;

namespace PosTpv.Domain.Entities;

/// <summary>A closed bill produced when an order is charged. Snapshots the money totals.</summary>
public class Invoice : BaseEntity
{
    public string Number { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal Total { get; set; }
    public PaymentMethod PaymentMethod { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
