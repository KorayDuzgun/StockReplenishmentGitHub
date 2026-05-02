using StockReplenishment.Contracts.Dtos.Articles;

namespace StockReplenishment.Data.Repositories;

/// <summary>Read-only access to the article catalog.</summary>
public interface IArticleRepository
{
    Task<IReadOnlyList<ArticleDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Returns true when every supplied id matches an existing article. Used to validate request items.</summary>
    Task<bool> AllExistAsync(IReadOnlyCollection<Guid> articleIds, CancellationToken cancellationToken);
}
