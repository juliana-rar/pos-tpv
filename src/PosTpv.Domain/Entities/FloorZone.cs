using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>
/// A named, spatial boundary drawn directly on the floor map (e.g. "Terrace", "Bar") — distinct
/// from <see cref="RestaurantTable.Zone"/>, which is just the free-text tag a table is filed
/// under. Purely a visual/clickable region: clicking it on the map applies the same zone filter
/// as the existing zone tabs: it doesn't itself own or constrain which tables are "inside" it.
/// </summary>
public class FloorZone : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double Width { get; set; } = 260;
    public double Height { get; set; } = 180;

    /// <summary>Optional accent colour (hex) tinting the zone's fill/border/label. Null uses the
    /// default neutral primary-colour tint.</summary>
    public string? Color { get; set; }
}
