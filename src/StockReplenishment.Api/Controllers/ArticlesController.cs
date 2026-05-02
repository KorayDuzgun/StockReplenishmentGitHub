using Microsoft.AspNetCore.Mvc;
using StockReplenishment.Data.Repositories;
using StockReplenishment.Contracts.Dtos.Articles;

namespace StockReplenishment.Api.Controllers;

/// <summary>Read-only access to the article catalog.</summary>
[ApiController]
[Route("api/articles")]
[Produces("application/json")]
public sealed class ArticlesController : ControllerBase
{
    private readonly IArticleRepository _articles;

    public ArticlesController(IArticleRepository articles) => _articles = articles;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ArticleDto>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ArticleDto>> List(CancellationToken cancellationToken)
        => await _articles.ListAsync(cancellationToken);
}
