namespace PosTpv.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the real-time transport (SignalR) for the Suppliers screen. Implemented in
/// the Web layer so the Application layer stays free of SignalR dependencies.
/// </summary>
public interface ISupplierNotifier
{
    /// <summary>
    /// An albarán scan finished processing (or failed, or was validated). Lets the Suppliers
    /// page refresh that row without the user reloading — the vision-model call can take
    /// minutes, so the user may have navigated away and come back by the time it's done.
    /// </summary>
    Task AlbaranScanUpdatedAsync(int scanId, string status);
}
