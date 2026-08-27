namespace PosTpv.Application.DTOs;

public record SupplierDocumentDto(int Id, string FileName, string FileUrl, string? ContentType, long FileSize, DateTime CreatedAt);

public record SupplierDto(
    int Id, string Name, string? ContactName, string? Phone, string? Email, string? TaxId,
    string? Address, string? Notes, bool IsActive, int DocumentCount, int PurchaseCount);

public class SupplierFormDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public record PurchaseLineDto(int Id, int ProductId, string ProductName, decimal Quantity, decimal UnitCost, decimal LineTotal);

public record PurchaseDto(
    int Id, int SupplierId, string SupplierName, DateTime Date, string? Reference,
    string? Notes, decimal Total, IReadOnlyList<PurchaseLineDto> Lines);

public class PurchaseLineFormDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class PurchaseFormDto
{
    public int SupplierId { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseLineFormDto> Lines { get; set; } = new();
}

public record StockItemDto(int ProductId, string ProductName, string CategoryName, decimal StockQuantity);

public class StockAdjustFormDto
{
    public int ProductId { get; set; }
    public decimal NewQuantity { get; set; }
    public string? Note { get; set; }
}
