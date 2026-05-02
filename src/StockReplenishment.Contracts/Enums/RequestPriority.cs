namespace StockReplenishment.Contracts.Enums;

/// <summary>
/// Business priority of a replenishment request. Used for filtering and sorting in review queues.
/// </summary>
public enum RequestPriority
{
    Low = 0,
    Normal = 1,
    Urgent = 2
}
