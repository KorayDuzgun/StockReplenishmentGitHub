using System.Net;
using System.Threading.Channels;
using StockReplenishment.Contracts.Dtos.Common;
using StockReplenishment.Contracts.Dtos.Requests;
using StockReplenishment.Contracts.Enums;
using StockReplenishment.Data.Entities;
using StockReplenishment.Data.Persistence;
using StockReplenishment.Data.Repositories;
using StockReplenishment.Services.Abstractions;
using StockReplenishment.Services.Exceptions;

namespace StockReplenishment.Services;

/// <summary>
/// Application service that owns the workflow: authorization, validation, state transitions and
/// persistence around <see cref="ReplenishmentRequest"/>. Anemic domain — all transition rules
/// live here so the entity stays a plain POCO that EF can populate freely.
/// </summary>
internal sealed class ReplenishmentRequestService : IReplenishmentRequestService
{
    private readonly AppDbContext _db;
    private readonly IReplenishmentRequestRepository _requests;
    private readonly IStockLocationRepository _locations;
    private readonly IArticleRepository _articles;
    private readonly ChannelWriter<Guid> _availabilityQueue;
    private readonly ICurrentUser _currentUser;

    public ReplenishmentRequestService(
        AppDbContext db,
        IReplenishmentRequestRepository requests,
        IStockLocationRepository locations,
        IArticleRepository articles,
        Channel<Guid> availabilityQueue,
        ICurrentUser currentUser)
    {
        _db = db;
        _requests = requests;
        _locations = locations;
        _articles = articles;
        _availabilityQueue = availabilityQueue.Writer;
        _currentUser = currentUser;
    }

    public async Task<ReplenishmentRequestDto> CreateDraftAsync(CreateRequestDto input, CancellationToken cancellationToken)
    {
        RequireRole(UserRole.Worker);
        await EnsureLocationExistsAsync(input.StockLocationId, cancellationToken);
        await EnsureArticlesExistAsync(input.Items, cancellationToken);

        var request = new ReplenishmentRequest
        {
            Id = Guid.NewGuid(),
            RequestNumber = $"REP-{Guid.NewGuid().ToString("N").AsSpan(0, 8).ToString().ToUpperInvariant()}",
            StockLocationId = input.StockLocationId,
            Priority = input.Priority,
            Status = RequestStatus.Draft,
            StockAvailabilityCheckStatus = StockAvailabilityCheckStatus.NotStarted,
            CreatedBy = _currentUser.UserName,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = input.Items
                .Select(i => new ReplenishmentRequestItem
                {
                    Id = Guid.NewGuid(),
                    ArticleId = i.ArticleId,
                    RequestedQuantity = i.RequestedQuantity
                })
                .ToList()
        };

        _requests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);
        return await LoadDtoAsync(request.Id, cancellationToken);
    }

    public async Task<ReplenishmentRequestDto> UpdateDraftAsync(Guid id, UpdateRequestDto input, CancellationToken cancellationToken)
    {
        RequireRole(UserRole.Worker);
        
        await EnsureArticlesExistAsync(input.Items, cancellationToken);

        var request = await LoadForUpdateAsync(id, cancellationToken);
        RequireOwnership(request);
        
        EnsureStatus(request, RequestStatus.Draft, "update");

        request.Priority = input.Priority;
        request.Items.Clear();
        foreach (var i in input.Items)
        {
            request.Items.Add(new ReplenishmentRequestItem
            {
                Id = Guid.NewGuid(),
                RequestId = id,
                ArticleId = i.ArticleId,
                RequestedQuantity = i.RequestedQuantity
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadDtoAsync(id, cancellationToken);
    }

    public async Task SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        RequireRole(UserRole.Worker);

        var request = await LoadForUpdateAsync(id, cancellationToken);
        RequireOwnership(request);
        EnsureStatus(request, RequestStatus.Draft, "submit");
        if (request.Items.Count == 0)
            throw new BusinessException("Cannot submit a request with no items.", HttpStatusCode.UnprocessableEntity);

        request.Status = RequestStatus.Submitted;
        request.SubmittedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // Enqueue AFTER the transition is durably persisted; otherwise the worker could race and
        // see the old state. Cancellation isn't propagated: once submitted, the side-effect must run.
        await _availabilityQueue.WriteAsync(id, CancellationToken.None);
    }

    public async Task<ReplenishmentRequestDto> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        RequireRole(UserRole.Reviewer);

        var request = await LoadForUpdateAsync(id, cancellationToken);
        EnsureStatus(request, RequestStatus.Submitted, "approve");

        request.Status = RequestStatus.Approved;
        request.ReviewedBy = _currentUser.UserName;
        request.ApprovedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return await LoadDtoAsync(id, cancellationToken);
    }

    public async Task<ReplenishmentRequestDto> RejectAsync(Guid id, RejectRequestDto input, CancellationToken cancellationToken)
    {
        RequireRole(UserRole.Reviewer);
        if (string.IsNullOrWhiteSpace(input.Reason))
            throw new BusinessException("Rejection reason is required.", HttpStatusCode.BadRequest);

        var request = await LoadForUpdateAsync(id, cancellationToken);
        EnsureStatus(request, RequestStatus.Submitted, "reject");

        request.Status = RequestStatus.Rejected;
        request.StockAvailabilityCheckStatus = StockAvailabilityCheckStatus.Completed;
        request.ReviewedBy = _currentUser.UserName;
        request.RejectionReason = input.Reason.Trim();
        request.RejectedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return await LoadDtoAsync(id, cancellationToken);
    }

    public async Task<ReplenishmentRequestDto> FulfillAsync(Guid id, FulfillRequestDto input, CancellationToken cancellationToken)
    {
        RequireRole(UserRole.Reviewer);

        var request = await LoadForUpdateAsync(id, cancellationToken);
        EnsureStatus(request, RequestStatus.Approved, "fulfill");

        // Build a defensive dictionary up-front: catches duplicate ItemId entries instead of
        // letting a later one silently overwrite an earlier one.
        var fulfillments = new Dictionary<Guid, int>(input.Items.Count);
        foreach (var item in input.Items)
        {
            if (!fulfillments.TryAdd(item.ItemId, item.FulfilledQuantity))
                throw new BusinessException($"Duplicate fulfilment entry for item '{item.ItemId}'.", HttpStatusCode.BadRequest);
        }

        foreach (var item in request.Items)
        {
            if (!fulfillments.TryGetValue(item.Id, out var qty))
                throw new BusinessException($"Fulfilled quantity missing for item '{item.Id}'.", HttpStatusCode.BadRequest);
            if (qty < 0)
                throw new BusinessException("Fulfilled quantity cannot be negative.", HttpStatusCode.BadRequest);
            if (qty > item.RequestedQuantity)
                throw new BusinessException(
                    $"Fulfilled quantity ({qty}) exceeds requested ({item.RequestedQuantity}) for item '{item.Id}'.",
                    HttpStatusCode.UnprocessableEntity);

            item.FulfilledQuantity = qty;
        }

        request.Status = RequestStatus.Fulfilled;
        request.ReviewedBy = _currentUser.UserName;
        request.FulfilledAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return await LoadDtoAsync(id, cancellationToken);
    }

    public Task<ReplenishmentRequestDto> GetAsync(Guid id, CancellationToken cancellationToken)
        => LoadDtoAsync(id, cancellationToken);

    public Task<PagedResult<RequestListItemDto>> ListAsync(RequestQuery query, CancellationToken cancellationToken)
    {
        // Row-level security: workers can only see their own requests, regardless of what the
        // client passed. Reviewers can see everything (or honour an explicit filter if provided).
        if (_currentUser.IsAuthenticated && _currentUser.Role == UserRole.Worker)
            query.CreatedBy = _currentUser.UserName;

        return _requests.ListAsync(query, cancellationToken);
    }

    // ---------- helpers ----------

    private async Task<ReplenishmentRequest> LoadForUpdateAsync(Guid id, CancellationToken cancellationToken)
        => await _requests.GetForUpdateAsync(id, cancellationToken)
           ?? throw new NotFoundException(nameof(ReplenishmentRequest), id);

    private async Task<ReplenishmentRequestDto> LoadDtoAsync(Guid id, CancellationToken cancellationToken)
        => await _requests.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException(nameof(ReplenishmentRequest), id);

    private void RequireRole(UserRole expected)
    {
        if (!_currentUser.IsAuthenticated)
            throw new BusinessException("A user identity is required for this operation.", HttpStatusCode.Forbidden);
        if (_currentUser.Role != expected)
            throw new BusinessException($"This operation requires role '{expected}'.", HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Worker write-actions (update, submit) must only target the worker's own drafts. Role-based
    /// authorisation alone is not enough — without this, any Worker who learns another worker's
    /// request id could submit or modify their draft.
    /// </summary>
    private void RequireOwnership(ReplenishmentRequest request)
    {
        if (!string.Equals(request.CreatedBy, _currentUser.UserName, StringComparison.Ordinal))
            throw new BusinessException("This request belongs to another user.", HttpStatusCode.Forbidden);
    }

    private async Task EnsureLocationExistsAsync(Guid locationId, CancellationToken cancellationToken)
    {
        if (!await _locations.ExistsAsync(locationId, cancellationToken))
            throw new NotFoundException(nameof(StockLocation), locationId);
    }

    private async Task EnsureArticlesExistAsync(IReadOnlyCollection<RequestItemInputDto> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0) return;
        var distinctIds = items.Select(i => i.ArticleId).ToHashSet();
        if (!await _articles.AllExistAsync(distinctIds, cancellationToken))
            throw new BusinessException("One or more articles do not exist in the catalog.", HttpStatusCode.BadRequest);
    }

    private static void EnsureStatus(ReplenishmentRequest request, RequestStatus expected, string operation)
    {
        if (request.Status != expected)
            throw new BusinessException(
                $"Cannot {operation} a request in status '{request.Status}'. Expected status '{expected}'.",
                HttpStatusCode.Conflict);
    }
}
