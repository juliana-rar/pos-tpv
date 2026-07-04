namespace PosTpv.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the real-time transport (SignalR). Implemented in the Web layer so the
/// Application layer stays free of SignalR dependencies.
/// </summary>
public interface IKitchenNotifier
{
    Task OrderSentToKitchenAsync(int orderId);
    Task OrderItemStatusChangedAsync(int orderId, int orderItemId);
    Task OrderReadyAsync(int orderId);

    /// <summary>
    /// The waiter fired (or the kitchen cleared) the second courses for an order. Both screens
    /// refresh so the highlight appears/disappears in real time.
    /// </summary>
    Task SecondCoursesFiredAsync(int orderId);
}
