using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>A single product/quantity/price line extracted from a scanned <see cref="AlbaranScan"/>.</summary>
public class AlbaranScanLine : BaseEntity
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public int AlbaranScanId { get; set; }
    public AlbaranScan AlbaranScan { get; set; } = null!;

    /// <summary>Set once the user maps this line to a real Product (required to create a Purchase).</summary>
    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
