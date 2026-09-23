using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>An optional add-on that can be attached to products and order lines (extra cheese, ...).</summary>
public class Extra : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();

    /// <summary>Categories this extra is assigned to in bulk — applies to every product in them
    /// (minus <see cref="ExcludedProducts"/>), on top of whatever's directly in <see cref="Products"/>.</summary>
    public ICollection<Category> Categories { get; set; } = new List<Category>();

    /// <summary>Products that opt out of a category-level assignment above (e.g. this extra
    /// applies to all Pizzas except the calzone). No effect on products reached via <see cref="Products"/>.</summary>
    public ICollection<Product> ExcludedProducts { get; set; } = new List<Product>();
}
