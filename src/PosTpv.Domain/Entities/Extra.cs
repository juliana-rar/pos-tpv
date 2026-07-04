using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>An optional add-on that can be attached to products and order lines (extra cheese, ...).</summary>
public class Extra : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
