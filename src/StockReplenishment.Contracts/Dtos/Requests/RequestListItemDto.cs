using StockReplenishment.Contracts.Enums;

namespace StockReplenishment.Contracts.Dtos.Requests;

/// <summary>
/// Lightweight projection used for <c>GET /api/requests</c>. Excludes line items and large fields
/// so list rendering stays fast even for large datasets.
/// </summary>
public sealed record RequestListItemDto(
    Guid Id,
    string RequestNumber,
    string StockLocationCode,
    RequestPriority Priority,
    RequestStatus Status,
    StockAvailabilityCheckStatus StockAvailabilityCheckStatus,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    int ItemCount);
