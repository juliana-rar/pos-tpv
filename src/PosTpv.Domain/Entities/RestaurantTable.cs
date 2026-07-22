using PosTpv.Domain.Common;
using PosTpv.Domain.Enums;

namespace PosTpv.Domain.Entities;

/// <summary>A table on the floor map, with geometry so it can be positioned and rotated freely.</summary>
public class RestaurantTable : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int Seats { get; set; } = 4;
    public TableShape Shape { get; set; } = TableShape.Square;
    public TableStatus Status { get; set; } = TableStatus.Available;

    /// <summary>Free-text floor area (e.g. "Main hall", "Bar") used to group/filter tables. Null = unassigned.</summary>
    public string? Zone { get; set; }

    /// <summary>Optional custom accent colour (hex) shown as a small badge on the floor plan,
    /// independent of the status-driven border colour — e.g. to flag a VIP table.</summary>
    public string? Color { get; set; }

    // Floor-map geometry (pixels on the plan canvas).
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double Width { get; set; } = 90;
    public double Height { get; set; } = 90;
    public double Rotation { get; set; }
    public bool IsLocked { get; set; }

    /// <summary>
    /// Tables sharing a non-null GroupId are joined and served as one unit with a single order.
    /// The lowest-Id member of a group is treated as the primary (it holds the order).
    /// </summary>
    public int? GroupId { get; set; }

    /// <summary>Display name for the joined group (shared by every member). Null while ungrouped.</summary>
    public string? GroupName { get; set; }

    /// <summary>
    /// Geometry captured the moment this table was first joined, so Separate can restore its
    /// pre-join disposition. Null while the table is not part of a group.
    /// </summary>
    public double? PreJoinPositionX { get; set; }
    public double? PreJoinPositionY { get; set; }
    public double? PreJoinHeight { get; set; }
    public double? PreJoinRotation { get; set; }
    public bool? PreJoinIsLocked { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
