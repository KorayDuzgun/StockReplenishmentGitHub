using StockReplenishment.Contracts.Enums;

namespace StockReplenishment.Contracts.Dtos.Requests;

/// <summary>Filter + pagination parameters for <c>GET /api/requests</c>.</summary>
public sealed class RequestQuery
{
    public RequestStatus? Status { get; set; }
    public RequestPriority? Priority { get; set; }
    public Guid? StockLocationId { get; set; }

    /// <summary>
    /// Restricts results to requests created by this user. Workers are scoped to their own requests
    /// at the service layer regardless of the value sent by the client (row-level security).
    /// </summary>
    public string? CreatedBy { get; set; }

    private int _page = 1;
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private int _pageSize = DefaultPageSize;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }
}
