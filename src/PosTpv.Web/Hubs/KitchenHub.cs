using Microsoft.AspNetCore.SignalR;

namespace PosTpv.Web.Hubs;

/// <summary>
/// Real-time channel shared by the POS, waiter and kitchen screens. The server broadcasts
/// order events; clients refresh the affected view without polling.
/// </summary>
public class KitchenHub : Hub
{
    public const string Path = "/hubs/kitchen";

    // Client-invokable event names (kept as constants so the notifier and the pages agree).
    public const string OrderSent = "OrderSent";
    public const string ItemStatusChanged = "ItemStatusChanged";
    public const string OrderReady = "OrderReady";
    public const string SecondsFired = "SecondsFired";
}
