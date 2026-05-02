using System.ComponentModel.DataAnnotations;

namespace StockReplenishment.Contracts.Dtos.Requests;

/// <summary>Input shape for a single line item when creating or updating a draft.</summary>
public sealed class RequestItemInputDto
{
    [Required]
    public Guid ArticleId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Requested quantity must be greater than zero.")]
    public int RequestedQuantity { get; set; }
}
