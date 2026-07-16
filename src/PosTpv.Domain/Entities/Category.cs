using PosTpv.Domain.Common;
using PosTpv.Domain.Enums;

namespace PosTpv.Domain.Entities;

/// <summary>Product grouping shown in the left rail of the POS (Drinks, Pizzas, ...).</summary>
public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether this category is prepared in the kitchen (food) or at the bar (drink).</summary>
    public CategoryKind Kind { get; set; } = CategoryKind.Food;

    /// <summary>Meal course this category maps to on the order screen (ignored for drinks).</summary>
    public CourseType Course { get; set; } = CourseType.Main;

    public string? Icon { get; set; }
    public string Color { get; set; } = "#6366f1";
    public string? ImageUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();

    /// <summary>Quick-pick order-line notes offered for products in this category.</summary>
    public ICollection<CategoryComment> Comments { get; set; } = new List<CategoryComment>();
}
