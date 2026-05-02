namespace StockReplenishment.Data.Entities;

/// <summary>Physical location on the shop floor (e.g. an assembly line). Read-only master data.</summary>
public sealed class StockLocation
{
    public Guid Id { get; set; }

    /// <summary>Short, human-friendly code (e.g. <c>LINE-A</c>).</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;
}
