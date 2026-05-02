namespace StockReplenishment.Contracts.Dtos.Requests;

/// <summary>Read model for a single line item on a request.</summary>
public sealed record ReplenishmentRequestItemDto(
    Guid Id,
    Guid ArticleId,
    string ArticleNumber,
    string Description,
    string Unit,
    int RequestedQuantity,
    int? AvailableQuantity,
    int? FulfilledQuantity);
