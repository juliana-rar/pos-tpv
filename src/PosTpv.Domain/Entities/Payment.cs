using PosTpv.Domain.Common;
using PosTpv.Domain.Enums;

namespace PosTpv.Domain.Entities;

/// <summary>An individual payment against an invoice (supports split payments).</summary>
public class Payment : BaseEntity
{
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
}
