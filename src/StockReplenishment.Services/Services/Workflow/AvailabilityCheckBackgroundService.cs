using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockReplenishment.Contracts.Enums;
using StockReplenishment.Data.Persistence;
using StockReplenishment.Services.Abstractions;

namespace StockReplenishment.Services.Workflow;

/// <summary>
/// Drains the availability-check queue and applies the external service result onto the request.
/// A fresh DI scope is created per work item because <see cref="DbContext"/> is not thread-safe.
/// Exceptions are caught and recorded as a failed availability check so a single bad request
/// never tears down the host.
/// </summary>
internal sealed class AvailabilityCheckBackgroundService : BackgroundService
{
    private readonly ChannelReader<Guid> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AvailabilityCheckBackgroundService> _logger;

    public AvailabilityCheckBackgroundService(
        Channel<Guid> queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AvailabilityCheckBackgroundService> logger)
    {
        _queue = queue.Reader;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Availability check worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid requestId;
            try
            {
                requestId = await _queue.ReadAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProcessAsync(requestId, stoppingToken);
        }

        _logger.LogInformation("Availability check worker stopping.");
    }

    private async Task ProcessAsync(Guid requestId, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var checker = scope.ServiceProvider.GetRequiredService<IStockAvailabilityChecker>();

        var request = await db.Requests.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == requestId, stoppingToken);
        if (request is null)
        {
            _logger.LogWarning("Availability check skipped: request {RequestId} no longer exists.", requestId);
            return;
        }

        try
        {
            request.AvailabilityCheckStatus = AvailabilityCheckStatus.InProgress;
            await db.SaveChangesAsync(stoppingToken);

            var result = await checker.CheckAsync(request, stoppingToken);

            foreach (var item in request.Items)
            {
                if (result.TryGetValue(item.Id, out var qty))
                    item.AvailableQuantity = qty;
            }
            request.AvailabilityCheckStatus = AvailabilityCheckStatus.Completed;
            await db.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("Availability check completed for request {RequestId}.", requestId);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down; leave the request InProgress so a future run can pick it up.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Availability check failed for request {RequestId}.", requestId);

            try
            {
                request.AvailabilityCheckStatus = AvailabilityCheckStatus.Failed;
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception persistEx)
            {
                _logger.LogError(persistEx, "Failed to persist availability failure for request {RequestId}.", requestId);
            }
        }
    }
}
