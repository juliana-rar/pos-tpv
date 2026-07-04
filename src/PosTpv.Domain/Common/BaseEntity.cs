namespace PosTpv.Domain.Common;

/// <summary>
/// Base type for all persisted entities. Provides an identity and audit timestamps.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
