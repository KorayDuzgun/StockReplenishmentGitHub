using System.ComponentModel.DataAnnotations;

namespace StockReplenishment.Contracts.Dtos.Requests;

/// <summary>Input body for <c>POST /api/requests/{id}/reject</c>. Reason is mandatory.</summary>
public sealed class RejectRequestDto
{
    [Required]
    [MinLength(1, ErrorMessage = "Rejection reason is required.")]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
