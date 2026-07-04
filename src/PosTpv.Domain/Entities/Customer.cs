using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>A known customer, optionally linked to reservations.</summary>
public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
