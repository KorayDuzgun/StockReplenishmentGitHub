using Microsoft.EntityFrameworkCore;
using StockReplenishment.Contracts.Dtos.Common;
using StockReplenishment.Contracts.Dtos.Requests;
using StockReplenishment.Data.Entities;
using StockReplenishment.Data.Persistence;

namespace StockReplenishment.Data.Repositories;

internal sealed class ReplenishmentRequestRepository : IReplenishmentRequestRepository
{
    private readonly AppDbContext _db;

    public ReplenishmentRequestRepository(AppDbContext db) => _db = db;

    public void Add(ReplenishmentRequest request) => _db.Requests.Add(request);

    public Task<ReplenishmentRequest?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
        => _db.Requests
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<ReplenishmentRequestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await _db.Requests
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new ReplenishmentRequestDto(
                r.Id,
                r.RequestNumber,
                r.StockLocationId,
                r.StockLocation!.Code,
                r.StockLocation.Name,
                r.Priority,
                r.Status,
                r.StockAvailabilityCheckStatus,
                r.CreatedBy,
                r.ReviewedBy,
                r.CreatedAt,
                r.SubmittedAt,
                r.ApprovedAt,
                r.RejectedAt,
                r.FulfilledAt,
                r.RejectionReason,
                r.Items.Select(i => new ReplenishmentRequestItemDto(
                    i.Id,
                    i.ArticleId,
                    i.Article!.ArticleNumber,
                    i.Article.Description,
                    i.Article.Unit,
                    i.RequestedQuantity,
                    i.AvailableQuantity,
                    i.FulfilledQuantity)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<RequestListItemDto>> ListAsync(RequestQuery query, CancellationToken cancellationToken)
    {
        var source = _db.Requests.AsNoTracking().AsQueryable();

        if (query.Status is { } status)                source = source.Where(r => r.Status == status);
        if (query.Priority is { } priority)            source = source.Where(r => r.Priority == priority);
        if (query.StockLocationId is { } locationId)   source = source.Where(r => r.StockLocationId == locationId);
        if (!string.IsNullOrWhiteSpace(query.CreatedBy)) source = source.Where(r => r.CreatedBy == query.CreatedBy);

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .OrderByDescending(r => r.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new RequestListItemDto(
                r.Id,
                r.RequestNumber,
                r.StockLocation!.Code,
                r.Priority,
                r.Status,
                r.StockAvailabilityCheckStatus,
                r.CreatedBy,
                r.CreatedAt,
                r.Items.Count))
            .ToListAsync(cancellationToken);

        return new PagedResult<RequestListItemDto>(items, query.Page, query.PageSize, totalCount);
    }
}
