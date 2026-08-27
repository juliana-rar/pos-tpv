using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>A file (invoice, contract, delivery note...) attached to a supplier.</summary>
public class SupplierDocument : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
}
