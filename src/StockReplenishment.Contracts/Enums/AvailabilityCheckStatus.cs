namespace StockReplenishment.Contracts.Enums;

/// <summary>
/// Status of the asynchronous external stock availability check.
/// Tracked independently from <see cref="RequestStatus"/> so that the slow side-effect
/// does not pollute the workflow state machine.
/// </summary>
public enum AvailabilityCheckStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3
}
