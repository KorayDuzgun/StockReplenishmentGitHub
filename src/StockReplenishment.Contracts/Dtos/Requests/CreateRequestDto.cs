using System.ComponentModel.DataAnnotations;
using StockReplenishment.Contracts.Enums;

namespace StockReplenishment.Contracts.Dtos.Requests;

/// <summary>Input body for <c>POST /api/requests</c>.</summary>
public sealed class CreateRequestDto
{
    [Required]
    public Guid StockLocationId { get; set; }

    [Required]
    public RequestPriority Priority { get; set; }

    /// <summary>At least one item is required to create a meaningful request, but it can be empty (and added later via PUT).</summary>
    [Required]
    public List<RequestItemInputDto> Items { get; set; } = new();
}
