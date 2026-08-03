namespace PosTpv.Application.DTOs;

public record AppSettingsDto(string Title, TimeOnly LunchStart, TimeOnly LunchEnd, TimeOnly DinnerStart, TimeOnly DinnerEnd, string FloorTexture, string ReservationPolicy, string PrimaryColor);

public class AppSettingsFormDto
{
    public string Title { get; set; } = "PosTPV";
    public TimeOnly LunchStart { get; set; } = new(13, 0);
    public TimeOnly LunchEnd { get; set; } = new(16, 0);
    public TimeOnly DinnerStart { get; set; } = new(20, 0);
    public TimeOnly DinnerEnd { get; set; } = new(23, 30);
    public string FloorTexture { get; set; } = "grid";
    public string ReservationPolicy { get; set; } = "open";
    public string PrimaryColor { get; set; } = "#6366f1";
}

/// <summary>Current-service-period lookup shared by the dashboard badge and the reservation
/// wizard's Lunch/Dinner step, so both read the same lunch/dinner windows configured in Settings.</summary>
public enum ServicePeriod { Closed, Lunch, Dinner }

public static class AppSettingsExtensions
{
    public static ServicePeriod GetPeriod(this AppSettingsDto s, TimeOnly time)
    {
        if (time >= s.LunchStart && time <= s.LunchEnd) return ServicePeriod.Lunch;
        if (time >= s.DinnerStart && time <= s.DinnerEnd) return ServicePeriod.Dinner;
        return ServicePeriod.Closed;
    }
}
