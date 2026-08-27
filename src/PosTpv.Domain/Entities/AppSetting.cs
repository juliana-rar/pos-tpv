using PosTpv.Domain.Common;

namespace PosTpv.Domain.Entities;

/// <summary>
/// Single-row table holding app-wide configurable settings (branding, service hours).
/// There is always exactly one row; the application-layer settings service creates it on
/// first read if missing.
/// </summary>
public class AppSetting : BaseEntity
{
    public string Title { get; set; } = "PosTPV";

    public TimeOnly LunchStart { get; set; } = new(13, 0);
    public TimeOnly LunchEnd { get; set; } = new(16, 0);
    public TimeOnly DinnerStart { get; set; } = new(20, 0);
    public TimeOnly DinnerEnd { get; set; } = new(23, 30);

    /// <summary>Floor-plan background style on /tables ("grid", "wood", "tile", "concrete").</summary>
    public string FloorTexture { get; set; } = "grid";

    /// <summary>App-wide accent colour (hex, e.g. "#6366f1") used for primary buttons, links and
    /// highlights across every screen.</summary>
    public string PrimaryColor { get; set; } = "#6366f1";

    /// <summary>How new reservations are currently being taken ("open", "phone_only", "closed",
    /// "web" — the last one reserved for when online booking exists).</summary>
    public string ReservationPolicy { get; set; } = "open";

    /// <summary>Legal/fiscal business name printed on the sales receipt. Falls back to
    /// <see cref="Title"/> when blank, since most small businesses trade under the same name.</summary>
    public string? ReceiptLegalName { get; set; }
    public string? ReceiptTaxId { get; set; }
    public string? ReceiptAddress { get; set; }
    public string? ReceiptFooter { get; set; }
    public bool ReceiptShowTaxBreakdown { get; set; } = true;

    /// <summary>Thermal paper width in millimetres for the printed receipt ("58" or "80").</summary>
    public string ReceiptPaperWidth { get; set; } = "80";
}
