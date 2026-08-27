namespace PosTpv.Domain.Enums;

/// <summary>Application roles that gate access to screens and actions.</summary>
public enum UserRole
{
    Admin = 0,
    Waiter = 1,
    Kitchen = 2,
    Cashier = 3
}

/// <summary>Physical shape used when rendering a table on the floor map.</summary>
public enum TableShape
{
    Square = 0,
    Round = 1,
    Rectangular = 2,
    Oval = 3
}

/// <summary>Lifecycle state of a table on the floor.</summary>
public enum TableStatus
{
    Available = 0,
    Occupied = 1,
    Reserved = 2,
    Locked = 3
}

/// <summary>Lifecycle state of a reservation.</summary>
public enum ReservationStatus
{
    Pending = 0,
    Confirmed = 1,
    Seated = 2,
    Finished = 3,
    Cancelled = 4
}

/// <summary>Lifecycle state of an order (comanda).</summary>
public enum OrderStatus
{
    Open = 0,
    Sent = 1,
    InPreparation = 2,
    Ready = 3,
    Delivered = 4,
    Paid = 5,
    Cancelled = 6
}

/// <summary>Preparation state of a single order line, driven by the kitchen display.</summary>
public enum OrderItemStatus
{
    Pending = 0,
    Preparing = 1,
    Ready = 2,
    Delivered = 3
}

/// <summary>
/// Preparation station a category belongs to. Food is cooked in the kitchen and appears on the
/// kitchen display (KDS); drinks are served from the bar and never reach the kitchen tickets.
/// </summary>
public enum CategoryKind
{
    Food = 0,
    Drink = 1
}

/// <summary>
/// Meal course a food category belongs to. Drives the block a line is shown under on the
/// order screen (drinks are grouped separately via <see cref="CategoryKind.Drink"/>).
/// </summary>
public enum CourseType
{
    /// <summary>First course — starters/entrantes.</summary>
    Starter = 0,
    /// <summary>Second course — mains/segundos (the default for food).</summary>
    Main = 1,
    /// <summary>Desserts/postres.</summary>
    Dessert = 2
}

/// <summary>Supported payment methods.</summary>
public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    Other = 2
}

/// <summary>Why a product's stock quantity changed.</summary>
public enum StockMovementReason
{
    Purchase = 0,
    Sale = 1,
    Adjustment = 2
}

/// <summary>
/// Non-table decoration/architecture element placed on the floor map — either a decorative plant
/// or an interior-design element (wall, door, bar, column, window) used to sketch the room's real
/// layout instead of a bare grid of tables.
/// </summary>
public enum FloorDecorType
{
    PottedPlant = 0,
    SmallPlant = 1,
    HangingPlant = 2,
    Wall = 3,
    Door = 4,
    BarCounter = 5,
    Column = 6,
    Window = 7,
    Bush = 8,
    SmallTree = 9,
    Fern = 10
}
