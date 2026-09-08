using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.Services;

namespace PosTpv.Web.Services;

/// <summary>
/// Fires off an albarán-scan vision-model call without blocking the Blazor Server circuit that
/// triggered it (CPU-only inference can take minutes). Runs each scan in its own DI scope, since
/// the caller's scope (and its DbContext) doesn't outlive the click that started it.
/// </summary>
public class AlbaranScanBackgroundRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlbaranScanBackgroundRunner> _logger;

    public AlbaranScanBackgroundRunner(IServiceScopeFactory scopeFactory, ILogger<AlbaranScanBackgroundRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void QueueScan(int scanId, string absoluteImagePath)
    {
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            try
            {
                var service = scope.ServiceProvider.GetRequiredService<IAlbaranScanService>();
                await service.ProcessAsync(scanId, absoluteImagePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Albarán scan {ScanId} failed unexpectedly.", scanId);
            }
        });
    }
}
