using Microsoft.EntityFrameworkCore;
using StockReplenishment.Contracts.Dtos.Locations;
using StockReplenishment.Data.Persistence;

namespace StockReplenishment.Data.Repositories;

internal sealed class StockLocationRepository : IStockLocationRepository
{
    private readonly AppDbContext _db;

    public StockLocationRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<StockLocationDto>> ListAsync(CancellationToken cancellationToken)
        => await _db.StockLocations
            .AsNoTracking()
            .OrderBy(l => l.Code)
            .Select(l => new StockLocationDto(l.Id, l.Code, l.Name))
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
        => _db.StockLocations.AsNoTracking().AnyAsync(l => l.Id == id, cancellationToken);
}
