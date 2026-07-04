using PosTpv.Domain.Common;
using PosTpv.Domain.Enums;

namespace PosTpv.Domain.Entities;

/// <summary>A staff member who can log in. Authentication is by username + PIN/password hash.</summary>
public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
