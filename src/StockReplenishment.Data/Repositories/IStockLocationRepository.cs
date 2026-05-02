using StockReplenishment.Contracts.Dtos.Locations;

namespace StockReplenishment.Data.Repositories;

/// <summary>Read-only access to stock locations.</summary>
public interface IStockLocationRepository
{
    Task<IReadOnlyList<StockLocationDto>> ListAsync(CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
