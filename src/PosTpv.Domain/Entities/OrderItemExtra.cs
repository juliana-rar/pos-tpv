using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>An extra applied to a specific order line, with its price captured at sale time.</summary>
public class OrderItemExtra : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public int OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;

    public int? ExtraId { get; set; }
    public Extra? Extra { get; set; }
}
