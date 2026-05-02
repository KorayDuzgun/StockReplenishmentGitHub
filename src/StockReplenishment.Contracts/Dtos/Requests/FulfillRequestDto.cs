using System.ComponentModel.DataAnnotations;

namespace StockReplenishment.Contracts.Dtos.Requests;

/// <summary>Input body for <c>POST /api/requests/{id}/fulfill</c>.</summary>
public sealed class FulfillRequestDto
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one item must be provided.")]
    public List<FulfillItemDto> Items { get; set; } = new();
}

public sealed class FulfillItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Fulfilled quantity cannot be negative.")]
    public int FulfilledQuantity { get; set; }
}
