using Microsoft.AspNetCore.SignalR;
using PosTpv.Application.Common.Interfaces;

namespace PosTpv.Web.Hubs;

/// <summary>SignalR-backed implementation of the application-layer supplier notifier.</summary>
public class SignalRSupplierNotifier : ISupplierNotifier
{
    private readonly IHubContext<SuppliersHub> _hub;

    public SignalRSupplierNotifier(IHubContext<SuppliersHub> hub) => _hub = hub;

    public Task AlbaranScanUpdatedAsync(int scanId, string status) =>
        _hub.Clients.All.SendAsync(SuppliersHub.AlbaranScanUpdated, scanId, status);
}
