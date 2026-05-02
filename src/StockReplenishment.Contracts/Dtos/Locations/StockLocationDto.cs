namespace StockReplenishment.Contracts.Dtos.Locations;

/// <summary>Read model for stock locations exposed by <c>GET /api/locations</c>.</summary>
public sealed record StockLocationDto(Guid Id, string Code, string Name);
