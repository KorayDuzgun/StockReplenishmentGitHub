namespace StockReplenishment.Contracts.Dtos.Articles;

/// <summary>Read model for catalog articles exposed by <c>GET /api/articles</c>.</summary>
public sealed record ArticleDto(Guid Id, string ArticleNumber, string Description, string Unit);
