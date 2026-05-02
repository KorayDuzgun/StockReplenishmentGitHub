using System.ComponentModel.DataAnnotations;
using StockReplenishment.Contracts.Enums;

namespace StockReplenishment.Contracts.Dtos.Requests;

/// <summary>Input body for <c>PUT /api/requests/{id}</c>. Replaces priority and the entire item list.</summary>
public sealed class UpdateRequestDto
{
    [Required]
    public RequestPriority Priority { get; set; }

    [Required]
    public List<RequestItemInputDto> Items { get; set; } = new();
}
