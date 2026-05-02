using StockReplenishment.Data.Entities;

namespace StockReplenishment.Services.Abstractions;

/// <summary>
/// Abstraction over the slow external stock-check service. The simulated implementation lives in
/// Infrastructure and uses <c>Task.Delay</c> with a randomised duration; a real adapter would
/// call out to an ERP/WMS over HTTP. Returning a dictionary keyed by item id keeps the contract
/// stable regardless of how the upstream service shapes its response.
/// </summary>
public interface IStockAvailabilityChecker
{
    /// <summary>
    /// Returns the available quantity for each requested item. The key is the
    /// <see cref="ReplenishmentRequestItem.Id"/> so the caller can apply results without a second lookup.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CheckAsync(ReplenishmentRequest request, CancellationToken cancellationToken);
}
