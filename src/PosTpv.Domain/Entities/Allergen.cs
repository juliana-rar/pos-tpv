using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>A food allergen (gluten, dairy, nuts...) that can be tagged on products.</summary>
public class Allergen : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
