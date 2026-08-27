using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>A goods/ingredient supplier. Purchases are recorded against it and it can hold
/// attached documents (invoices, contracts, delivery notes).</summary>
public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<SupplierDocument> Documents { get; set; } = new List<SupplierDocument>();
    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
}
