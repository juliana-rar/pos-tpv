namespace PosTpv.Domain.Common;

/// <summary>Implemented by entities with a manually-swappable display position (menu products, categories, quick-pick comments).</summary>
public interface IOrderable
{
    int DisplayOrder { get; set; }
}
