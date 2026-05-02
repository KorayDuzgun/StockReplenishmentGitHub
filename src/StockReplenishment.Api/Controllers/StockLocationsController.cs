using Microsoft.AspNetCore.Mvc;
using StockReplenishment.Data.Repositories;
using StockReplenishment.Contracts.Dtos.Locations;

namespace StockReplenishment.Api.Controllers;

/// <summary>Read-only access to stock locations.</summary>
[ApiController]
[Route("api/locations")]
[Produces("application/json")]
public sealed class StockLocationsController : ControllerBase
{
    private readonly IStockLocationRepository _locations;

    public StockLocationsController(IStockLocationRepository locations) => _locations = locations;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StockLocationDto>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<StockLocationDto>> List(CancellationToken cancellationToken)
        => await _locations.ListAsync(cancellationToken);
}
