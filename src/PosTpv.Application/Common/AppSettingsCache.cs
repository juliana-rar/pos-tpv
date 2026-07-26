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
    // This is the one genuinely cross-request singleton in the app (every Blazor Server circuit
    // shares the same instance), so reads/writes of Current are locked against concurrent Set()
    // calls from two admins saving settings at once.
    private readonly object _lock = new();
    private AppSettingsDto _current =
        new("PosTPV", new TimeOnly(13, 0), new TimeOnly(16, 0), new TimeOnly(20, 0), new TimeOnly(23, 30), "grid", "open");

    public AppSettingsDto Current { get { lock (_lock) return _current; } }

    /// <summary>Raised whenever settings are saved, so long-lived components (e.g. the sidebar
    /// brand, which persists across in-app navigation) can refresh without waiting for the
    /// visiting circuit's next full page load.</summary>
    public event Action? Changed;

    public void Set(AppSettingsDto dto)
    {
        lock (_lock) { _current = dto; }
        Changed?.Invoke();
    }
}
