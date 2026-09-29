using PosTpv.Domain.Common;
using PosTpv.Domain.Enums;

namespace PosTpv.Domain.Entities;

/// <summary>
/// A non-table element on the floor map — a decorative plant or an interior-design element
/// (wall, door, bar counter, column, window) — so the plan can be sketched out like a real
/// architectural floor plan instead of just a grid of tables. Shares the same free-form
/// position/size/rotation geometry as <see cref="RestaurantTable"/> and the same drag/resize/
/// rotate/lock editor on the client (see floorplan.js), but carries no business state of its own.
/// </summary>
public class FloorDecor : BaseEntity
{
    public FloorDecorType Type { get; set; }

    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double Width { get; set; } = 60;
    public double Height { get; set; } = 60;
    public double Rotation { get; set; }
    public bool IsLocked { get; set; }

    /// <summary>Default footprint per element type — a wall/bar reads as a long thin strip, a
    /// column/plant as a small square, matching how each would actually sit on a real floor plan.</summary>
    public static (double Width, double Height) DefaultSize(FloorDecorType type) => type switch
    {
        FloorDecorType.Wall => (160, 16),
        FloorDecorType.Door => (60, 14),
        FloorDecorType.BarCounter => (220, 50),
        FloorDecorType.Column => (36, 36),
        FloorDecorType.Window => (100, 14),
        FloorDecorType.SmallPlant => (36, 36),
        FloorDecorType.HangingPlant => (44, 44),
        FloorDecorType.Bush => (64, 64),
        FloorDecorType.SmallTree => (76, 76),
        FloorDecorType.Fern => (46, 46),
        FloorDecorType.GlassWall => (180, 10),
        FloorDecorType.Banquette => (140, 34),
        FloorDecorType.Rug => (180, 140),
        FloorDecorType.Planter => (140, 24),
        FloorDecorType.Parasol => (90, 90),
        FloorDecorType.WineRack => (160, 28),
        FloorDecorType.Kitchen => (240, 200),
        FloorDecorType.Restrooms => (160, 120),
        FloorDecorType.Stairs => (90, 150),
        FloorDecorType.HostStand => (46, 30),
        _ => (50, 50)
    };
}
