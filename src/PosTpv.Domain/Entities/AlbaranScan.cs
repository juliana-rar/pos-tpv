using PosTpv.Domain.Common;
using PosTpv.Domain.Enums;

namespace PosTpv.Domain.Entities;

/// <summary>
/// A supplier delivery note (albarán) scanned from a photo. Starts as a raw vision-model
/// extraction (<see cref="AlbaranScanStatus.Processing"/>/<see cref="AlbaranScanStatus.NeedsReview"/>)
/// and, once a person confirms it, becomes a real <see cref="Purchase"/>.
/// </summary>
public class AlbaranScan : BaseEntity
{
    public string ImageUrl { get; set; } = string.Empty;

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>Supplier name as read from the image, before it's matched to a real Supplier.</summary>
    public string? SupplierNameRaw { get; set; }

    public string? AlbaranNumber { get; set; }
    public DateTime? AlbaranDate { get; set; }
    public decimal? TotalAmount { get; set; }

    public AlbaranScanStatus Status { get; set; } = AlbaranScanStatus.Processing;

    /// <summary>Human-readable reason when <see cref="Status"/> is Failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Raw text returned by the vision model, kept for troubleshooting bad extractions.</summary>
    public string? RawModelResponse { get; set; }

    /// <summary>Set once this scan has been validated into a real Purchase.</summary>
    public int? PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public ICollection<AlbaranScanLine> Lines { get; set; } = new List<AlbaranScanLine>();
}
