using StockReplenishment.Contracts.Dtos.Common;
using StockReplenishment.Contracts.Dtos.Requests;

namespace StockReplenishment.Services.Abstractions;

/// <summary>Application service that orchestrates the replenishment workflow.</summary>
public interface IReplenishmentRequestService
{
    Task<ReplenishmentRequestDto> CreateDraftAsync(CreateRequestDto input, CancellationToken cancellationToken);
    Task<ReplenishmentRequestDto> UpdateDraftAsync(Guid id, UpdateRequestDto input, CancellationToken cancellationToken);
    Task SubmitAsync(Guid id, CancellationToken cancellationToken);
    Task<ReplenishmentRequestDto> ApproveAsync(Guid id, CancellationToken cancellationToken);
    Task<ReplenishmentRequestDto> RejectAsync(Guid id, RejectRequestDto input, CancellationToken cancellationToken);
    Task<ReplenishmentRequestDto> FulfillAsync(Guid id, FulfillRequestDto input, CancellationToken cancellationToken);
    Task<ReplenishmentRequestDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<RequestListItemDto>> ListAsync(RequestQuery query, CancellationToken cancellationToken);
}
