using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>A purchase (goods bought from a supplier). Confirming it restocks its lines' products.</summary>
public class Purchase : BaseEntity
{
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public ICollection<PurchaseLine> Lines { get; set; } = new List<PurchaseLine>();

    public decimal Total => Lines.Sum(l => l.Quantity * l.UnitCost);
}
