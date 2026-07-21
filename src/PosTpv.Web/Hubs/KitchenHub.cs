using Microsoft.AspNetCore.SignalR;

namespace PosTpv.Web.Hubs;

/// <summary>
/// Real-time channel shared by the POS, waiter and kitchen screens. The server broadcasts
/// order events; clients refresh the affected view without polling.
/// </summary>
/// <remarks>
/// Deliberately not [Authorize]: the Blazor Server components open this connection
/// server-side (no browser auth cookie to forward), and every page that connects here
/// (Pos, Orders, Kitchen) already requires a role via its own [Authorize] attribute, so
/// gating the hub too would only block the legitimate callers, not unauthenticated ones.
/// </remarks>
public class KitchenHub : Hub
{
    public const string Path = "/hubs/kitchen";

    // Client-invokable event names (kept as constants so the notifier and the pages agree).
    public const string OrderSent = "OrderSent";
    public const string ItemStatusChanged = "ItemStatusChanged";
    public const string OrderReady = "OrderReady";
    public const string FirstsFired = "FirstsFired";
    public const string SecondsFired = "SecondsFired";
    public const string DessertsFired = "DessertsFired";
    public const string SecondCoursesServed = "SecondCoursesServed";
    public const string DessertCoursesServed = "DessertCoursesServed";
    public const string DrinksServed = "DrinksServed";
    public const string FirstCoursesServed = "FirstCoursesServed";
}
