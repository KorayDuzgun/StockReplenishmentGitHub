using StockReplenishment.Contracts.Enums;

namespace StockReplenishment.Data.Entities;

/// <summary>
/// Replenishment request and its line items. State transitions and validation are owned by the
/// service layer (anemic model) — keep this type a plain POCO so EF Core can populate it freely.
/// </summary>
public sealed class ReplenishmentRequest
{
    public Guid Id { get; set; }

    /// <summary>Short, user-friendly identifier (e.g. <c>REP-3F7A91C2</c>).</summary>
    public string RequestNumber { get; set; } = default!;

    public Guid StockLocationId { get; set; }
    public StockLocation? StockLocation { get; set; }

    public RequestPriority Priority { get; set; }
    public RequestStatus Status { get; set; }
    public AvailabilityCheckStatus AvailabilityCheckStatus { get; set; }

    public string CreatedBy { get; set; } = default!;
    public string? ReviewedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public DateTimeOffset? FulfilledAt { get; set; }

    public string? RejectionReason { get; set; }

    public List<ReplenishmentRequestItem> Items { get; set; } = new();
}
