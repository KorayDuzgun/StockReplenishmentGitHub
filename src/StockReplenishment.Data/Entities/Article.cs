namespace StockReplenishment.Data.Entities;

/// <summary>Catalog item that can appear on a replenishment request line. Read-only master data.</summary>
public sealed class Article
{
    public Guid Id { get; set; }

    /// <summary>External-facing article identifier (e.g. <c>BLT-M8-25</c>).</summary>
    public string ArticleNumber { get; set; } = default!;

    public string Description { get; set; } = default!;

    /// <summary>Stocking unit (e.g. <c>PCS</c>, <c>KG</c>, <c>M</c>).</summary>
    public string Unit { get; set; } = default!;
}
