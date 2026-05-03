using Microsoft.AspNetCore.Mvc;
using StockReplenishment.Services.Abstractions;
using StockReplenishment.Contracts.Dtos.Common;
using StockReplenishment.Contracts.Dtos.Requests;

namespace StockReplenishment.Api.Controllers;

/// <summary>
/// HTTP surface for the replenishment workflow. Each endpoint is a thin call into
/// <see cref="IReplenishmentRequestService"/>; business rules and authorization live there.
/// </summary>
[ApiController]
[Route("api/requests")]
[Produces("application/json")]
public sealed class ReplenishmentRequestsController : ControllerBase
{
    private readonly IReplenishmentRequestService _service;

    public ReplenishmentRequestsController(IReplenishmentRequestService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RequestListItemDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<RequestListItemDto>> List([FromQuery] RequestQuery query, CancellationToken cancellationToken)
        => _service.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReplenishmentRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<ReplenishmentRequestDto> Get(Guid id, CancellationToken cancellationToken)
        => _service.GetAsync(id, cancellationToken);

    /// <summary>
    /// Polling endpoint for the asynchronous availability check. Returns the same DTO as
    /// <c>GET /api/requests/{id}</c>; clients read <c>StockAvailabilityCheckStatus</c> and the per-item
    /// <c>availableQuantity</c> values to decide when to stop polling.
    /// </summary>
    [HttpGet("{id:guid}/availability")]
    [ProducesResponseType(typeof(ReplenishmentRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<ReplenishmentRequestDto> GetAvailability(Guid id, CancellationToken cancellationToken)
        => _service.GetAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType(typeof(ReplenishmentRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ReplenishmentRequestDto>> Create(
        [FromBody] CreateRequestDto input,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateDraftAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ReplenishmentRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<ReplenishmentRequestDto> Update(
        Guid id,
        [FromBody] UpdateRequestDto input,
        CancellationToken cancellationToken)
        => _service.UpdateDraftAsync(id, input, cancellationToken);

    /// <summary>
    /// Submits the request for review. The slow availability check is enqueued to a background
    /// worker, so this endpoint returns immediately with <c>202 Accepted</c> and a Location pointing
    /// to the polling endpoint.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        await _service.SubmitAsync(id, cancellationToken);
        var pollUrl = Url.Action(nameof(GetAvailability), new { id })!;
        return Accepted(pollUrl);
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ReplenishmentRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<ReplenishmentRequestDto> Approve(Guid id, CancellationToken cancellationToken)
        => _service.ApproveAsync(id, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ReplenishmentRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<ReplenishmentRequestDto> Reject(
        Guid id,
        [FromBody] RejectRequestDto input,
        CancellationToken cancellationToken)
        => _service.RejectAsync(id, input, cancellationToken);

    [HttpPost("{id:guid}/fulfill")]
    [ProducesResponseType(typeof(ReplenishmentRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<ReplenishmentRequestDto> Fulfill(
        Guid id,
        [FromBody] FulfillRequestDto input,
        CancellationToken cancellationToken)
        => _service.FulfillAsync(id, input, cancellationToken);
}
