using StockReplenishment.Services.Abstractions;
using StockReplenishment.Data.Entities;

namespace StockReplenishment.Services.Workflow;

/// <summary>
/// Stand-in for the real ERP/WMS stock-check call. Adds a 2–5 second delay (so the asynchronous
/// design is observable in the UI), occasionally returns less than the requested quantity to
/// surface the "insufficient" branch, and very rarely throws to exercise the failure path.
/// </summary>
internal sealed class SimulatedStockAvailabilityChecker : IStockAvailabilityChecker
{
    private const int MinDelayMs = 2_000;
    private const int MaxDelayMs = 5_000;
    private const double FailureRate = 0.05;     // 5% of calls throw
    private const double InsufficientRate = 0.30; // 30% of items return less than requested

    public async Task<IReadOnlyDictionary<Guid, int>> CheckAsync(
        ReplenishmentRequest request,
        CancellationToken cancellationToken)
    {
        // A new Random per call avoids the cross-thread hazards of a shared instance and the cost
        // is negligible compared to the simulated network delay below.
        var random = new Random();

        await Task.Delay(random.Next(MinDelayMs, MaxDelayMs), cancellationToken);

        if (random.NextDouble() < FailureRate)
            throw new InvalidOperationException("Simulated upstream stock service failure.");

        var result = new Dictionary<Guid, int>(request.Items.Count);
        foreach (var item in request.Items)
        {
            var available = random.NextDouble() < InsufficientRate
                ? random.Next(0, item.RequestedQuantity)
                : item.RequestedQuantity;

            result[item.Id] = available;
        }

        return result;
    }
}
