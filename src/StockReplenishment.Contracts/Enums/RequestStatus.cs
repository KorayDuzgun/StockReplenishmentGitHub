namespace StockReplenishment.Contracts.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Entities.ReplenishmentRequest"/>.
/// Transitions are enforced by the aggregate root.
/// </summary>
public enum RequestStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Fulfilled = 4
}
