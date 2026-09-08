using Microsoft.AspNetCore.SignalR;

namespace PosTpv.Web.Hubs;

/// <summary>
/// Real-time channel for the Suppliers screen. Currently used to push albarán-scan status
/// updates (the vision-model call can take minutes, so the user may navigate away and back).
/// </summary>
/// <remarks>
/// Deliberately not [Authorize], same rationale as <see cref="KitchenHub"/>: the Blazor Server
/// component opens this connection server-side, and /suppliers already requires the Admin role
/// via its own [Authorize] attribute.
/// </remarks>
public class SuppliersHub : Hub
{
    public const string Path = "/hubs/suppliers";

    public const string AlbaranScanUpdated = "AlbaranScanUpdated";
}
