using PosTpv.Domain.Enums;

namespace PosTpv.Application.DTOs;

public record ReservationDto(
    int Id, string CustomerName, string? Phone, DateTime Date, TimeOnly Time,
    int DurationMinutes, int PartySize, string? Comments, string Color,
    ReservationStatus Status, IReadOnlyList<int> TableIds, IReadOnlyList<string> TableNames);

public class ReservationFormDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public TimeOnly Time { get; set; } = new(20, 0);
    public int DurationMinutes { get; set; } = 90;
    public int PartySize { get; set; } = 2;
    public string? Comments { get; set; }
    public string Color { get; set; } = "#6366f1";
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public List<int> TableIds { get; set; } = new();
}
