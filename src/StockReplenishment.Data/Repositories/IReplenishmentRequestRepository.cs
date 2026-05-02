using StockReplenishment.Contracts.Dtos.Common;
using StockReplenishment.Contracts.Dtos.Requests;
using StockReplenishment.Data.Entities;

namespace StockReplenishment.Data.Repositories;

/// <summary>Repository for the request aggregate. Splits write-side (tracked load) from read-side (projection).</summary>
public interface IReplenishmentRequestRepository
{
    void Add(ReplenishmentRequest request);

    /// <summary>Loads the request with its items tracked, ready for state-changing operations.</summary>
    Task<ReplenishmentRequest?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<ReplenishmentRequestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<RequestListItemDto>> ListAsync(RequestQuery query, CancellationToken cancellationToken);
}
