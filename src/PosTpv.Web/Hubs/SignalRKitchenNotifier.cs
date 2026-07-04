using Microsoft.AspNetCore.SignalR;
using PosTpv.Application.Common.Interfaces;

namespace PosTpv.Web.Hubs;

/// <summary>SignalR-backed implementation of the application-layer kitchen notifier.</summary>
public class SignalRKitchenNotifier : IKitchenNotifier
{
    private readonly IHubContext<KitchenHub> _hub;

    public SignalRKitchenNotifier(IHubContext<KitchenHub> hub) => _hub = hub;

    public Task OrderSentToKitchenAsync(int orderId) =>
        _hub.Clients.All.SendAsync(KitchenHub.OrderSent, orderId);

    public Task OrderItemStatusChangedAsync(int orderId, int orderItemId) =>
        _hub.Clients.All.SendAsync(KitchenHub.ItemStatusChanged, orderId, orderItemId);

    public Task OrderReadyAsync(int orderId) =>
        _hub.Clients.All.SendAsync(KitchenHub.OrderReady, orderId);

    public Task SecondCoursesFiredAsync(int orderId) =>
        _hub.Clients.All.SendAsync(KitchenHub.SecondsFired, orderId);
}
