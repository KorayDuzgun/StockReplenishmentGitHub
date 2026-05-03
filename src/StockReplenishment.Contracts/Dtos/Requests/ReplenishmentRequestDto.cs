using StockReplenishment.Contracts.Enums;

namespace StockReplenishment.Contracts.Dtos.Requests;

/// <summary>Detailed read model for a single request, returned by <c>GET /api/requests/{id}</c>.</summary>
public sealed record ReplenishmentRequestDto(
    Guid Id,
    string RequestNumber,
    Guid StockLocationId,
    string StockLocationCode,
    string StockLocationName,
    RequestPriority Priority,
    RequestStatus Status,
    StockAvailabilityCheckStatus StockAvailabilityCheckStatus,
    string CreatedBy,
    string? ReviewedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? RejectedAt,
    DateTimeOffset? FulfilledAt,
    string? RejectionReason,
    IReadOnlyList<ReplenishmentRequestItemDto> Items);
