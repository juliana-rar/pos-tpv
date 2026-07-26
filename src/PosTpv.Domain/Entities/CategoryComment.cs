using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>A quick-pick order-line note scoped to a category (e.g. "Well done", "No cheese").</summary>
public class CategoryComment : BaseEntity, IOrderable
{
    public string Text { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}
