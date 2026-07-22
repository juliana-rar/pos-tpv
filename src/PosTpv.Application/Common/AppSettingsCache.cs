using PosTpv.Application.DTOs;

namespace PosTpv.Application.Common;

/// <summary>
/// In-memory snapshot of the single-row app settings (branding + service hours), so every
/// page can read the current values synchronously without a DB round-trip per render.
/// Registered as a singleton; refreshed by <see cref="Services.SettingsService"/> whenever
/// settings are read or saved.
/// </summary>
public class AppSettingsCache
{
    public AppSettingsDto Current { get; private set; } =
        new("PosTPV", new TimeOnly(13, 0), new TimeOnly(16, 0), new TimeOnly(20, 0), new TimeOnly(23, 30), "grid");

    /// <summary>Raised whenever settings are saved, so long-lived components (e.g. the sidebar
    /// brand, which persists across in-app navigation) can refresh without waiting for the
    /// visiting circuit's next full page load.</summary>
    public event Action? Changed;

    public void Set(AppSettingsDto dto)
    {
        Current = dto;
        Changed?.Invoke();
    }
}
