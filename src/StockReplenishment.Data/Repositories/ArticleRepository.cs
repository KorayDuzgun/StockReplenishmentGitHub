using Microsoft.EntityFrameworkCore;
using StockReplenishment.Contracts.Dtos.Articles;
using StockReplenishment.Data.Persistence;

namespace StockReplenishment.Data.Repositories;

internal sealed class ArticleRepository : IArticleRepository
{
    private readonly AppDbContext _db;

    public ArticleRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ArticleDto>> ListAsync(CancellationToken cancellationToken)
        => await _db.Articles
            .AsNoTracking()
            .OrderBy(a => a.ArticleNumber)
            .Select(a => new ArticleDto(a.Id, a.ArticleNumber, a.Description, a.Unit))
            .ToListAsync(cancellationToken);

    public async Task<bool> AllExistAsync(IReadOnlyCollection<Guid> articleIds, CancellationToken cancellationToken)
    {
        if (articleIds.Count == 0) return true;

        var foundCount = await _db.Articles
            .AsNoTracking()
            .CountAsync(a => articleIds.Contains(a.Id), cancellationToken);

        return foundCount == articleIds.Count;
    }
}
