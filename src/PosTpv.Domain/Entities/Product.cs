using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>A sellable item. Rendered as a card in the POS centre panel.</summary>
public class Product : BaseEntity, IOrderable
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal VatRate { get; set; } = 10m; // percentage, e.g. 10 = 10%
    public string Color { get; set; } = "#6366f1";
    public string? ImageUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
    public int PreparationMinutes { get; set; }
    public string? Ingredients { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public ICollection<Extra> Extras { get; set; } = new List<Extra>();
    public ICollection<Allergen> Allergens { get; set; } = new List<Allergen>();
}
