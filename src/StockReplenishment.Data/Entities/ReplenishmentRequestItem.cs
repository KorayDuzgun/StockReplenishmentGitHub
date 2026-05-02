namespace StockReplenishment.Data.Entities;

/// <summary>A single material line on a <see cref="ReplenishmentRequest"/>.</summary>
public sealed class ReplenishmentRequestItem
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }

    public Guid ArticleId { get; set; }

    public Article? Article { get; set; }

    public int RequestedQuantity { get; set; }

    /// <summary>Set by the external availability check; <c>null</c> until the check completes.</summary>
    public int? AvailableQuantity { get; set; }

    /// <summary>Set when the request transitions to <c>Fulfilled</c>; <c>null</c> until then.</summary>
    public int? FulfilledQuantity { get; set; }
}
