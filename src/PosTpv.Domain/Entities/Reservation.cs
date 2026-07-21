using PosTpv.Domain.Common;
using PosTpv.Domain.Enums;

namespace PosTpv.Domain.Entities;

/// <summary>
/// A table booking. A reserved table is not released automatically; it stays reserved
/// until the resulting order is fully paid (enforced in the application layer).
/// </summary>
public class Reservation : BaseEntity
{
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime Date { get; set; }
    public TimeOnly Time { get; set; }
    public int DurationMinutes { get; set; } = 90;
    public int PartySize { get; set; } = 2;
    public string? Comments { get; set; }
    public string Color { get; set; } = "#6366f1";
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public int ChildrenCount { get; set; }
    public int HighChairCount { get; set; }

    public ICollection<RestaurantTable> Tables { get; set; } = new List<RestaurantTable>();

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
}
