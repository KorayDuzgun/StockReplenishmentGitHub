using Microsoft.EntityFrameworkCore;
using StockReplenishment.Data.Entities;
using StockReplenishment.Data.Persistence;

namespace StockReplenishment.Tests.Helpers;

/// <summary>
/// Builds a fresh <see cref="AppDbContext"/> backed by a unique in-memory store per test so
/// individual cases stay isolated. Seeds a small fixed set of locations and articles so the
/// service under test has valid foreign keys to use.
/// </summary>
internal static class TestDb
{
    public static readonly Guid LocationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Article1Id = Guid.Parse("aaaaaaa1-0000-0000-0000-000000000001");
    public static readonly Guid Article2Id = Guid.Parse("aaaaaaa1-0000-0000-0000-000000000002");

    public static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var ctx = new AppDbContext(options);
        ctx.StockLocations.Add(new StockLocation { Id = LocationId, Code = "LINE-A", Name = "Assembly Line A" });
        ctx.Articles.Add(new Article { Id = Article1Id, ArticleNumber = "BLT-M8-25", Description = "Bolt M8x25", Unit = "PCS" });
        ctx.Articles.Add(new Article { Id = Article2Id, ArticleNumber = "NUT-M8", Description = "Nut M8", Unit = "PCS" });
        ctx.SaveChanges();
        return ctx;
    }
}
