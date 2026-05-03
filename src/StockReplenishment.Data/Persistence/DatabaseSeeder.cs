using Microsoft.EntityFrameworkCore;
using StockReplenishment.Data.Entities;
using StockReplenishment.Contracts.Enums;

namespace StockReplenishment.Data.Persistence;

/// <summary>
/// Populates the in-memory database with master data and a representative set of requests
/// covering every status so the reviewer can interact with the feature immediately on startup.
/// Idempotent: safe to call multiple times.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (await db.Articles.AnyAsync(cancellationToken)) return;

        var locations = SeedLocations();
        var articles = SeedArticles();

        await db.StockLocations.AddRangeAsync(locations, cancellationToken);
        await db.Articles.AddRangeAsync(articles, cancellationToken);
        await db.Requests.AddRangeAsync(SeedRequests(locations, articles), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static StockLocation[] SeedLocations() =>
    [
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Code = "LINE-A",   Name = "Assembly Line A" },
        new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Code = "LINE-B",   Name = "Assembly Line B" },
        new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Code = "ASSEMBLY", Name = "Final Assembly"  },
        new() { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Code = "PACK",     Name = "Packaging Station" },
        new() { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Code = "QC",       Name = "Quality Control" }
    ];

    private static Article[] SeedArticles() =>
    [
        new() { Id = Guid.Parse("aaaaaaa1-0000-0000-0000-000000000001"), ArticleNumber = "BLT-M8-25",  Description = "Bolt M8x25 stainless",   Unit = "PCS" },
        new() { Id = Guid.Parse("aaaaaaa1-0000-0000-0000-000000000002"), ArticleNumber = "BLT-M10-30", Description = "Bolt M10x30 stainless",  Unit = "PCS" },
        new() { Id = Guid.Parse("aaaaaaa1-0000-0000-0000-000000000003"), ArticleNumber = "NUT-M8",     Description = "Nut M8 stainless",       Unit = "PCS" },
        new() { Id = Guid.Parse("aaaaaaa1-0000-0000-0000-000000000004"), ArticleNumber = "NUT-M10",    Description = "Nut M10 stainless",      Unit = "PCS" },
        new() { Id = Guid.Parse("aaaaaaa1-0000-0000-0000-000000000005"), ArticleNumber = "WSH-M8",     Description = "Washer M8",              Unit = "PCS" },
        new() { Id = Guid.Parse("aaaaaaa1-0000-0000-0000-000000000006"), ArticleNumber = "BRK-L-200",  Description = "L-bracket 200mm",        Unit = "PCS" },
        new() { Id = Guid.Parse("aaaaaaa1-0000-0000-0000-000000000007"), ArticleNumber = "WIRE-2.5",   Description = "Wire 2.5mm² red",        Unit = "M"   },
        new() { Id = Guid.Parse("aaaaaaa1-0000-0000-0000-000000000008"), ArticleNumber = "WIRE-1.5",   Description = "Wire 1.5mm² blue",       Unit = "M"   },
        new() { Id = Guid.Parse("aaaaaaa1-0000-0000-0000-000000000009"), ArticleNumber = "GRS-IND",    Description = "Industrial grease",      Unit = "KG"  },
        new() { Id = Guid.Parse("aaaaaaa1-0000-0000-0000-00000000000A"), ArticleNumber = "BOX-S",      Description = "Cardboard box small",    Unit = "PCS" },
        new() { Id = Guid.Parse("aaaaaaa1-0000-0000-0000-00000000000B"), ArticleNumber = "BOX-L",      Description = "Cardboard box large",    Unit = "PCS" },
        new() { Id = Guid.Parse("aaaaaaa1-0000-0000-0000-00000000000C"), ArticleNumber = "TAPE-50",    Description = "Packing tape 50mm",      Unit = "PCS" }
    ];

    private static List<ReplenishmentRequest> SeedRequests(StockLocation[] locations, Article[] articles)
    {
        // Anchor "now" so seed timestamps are stable on every run rather than drifting per startup.
        var now = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var list = new List<ReplenishmentRequest>();

        // Draft (still being prepared by a worker)
        list.Add(Build(locations[0], RequestPriority.Normal, "john", now.AddMinutes(-30),
            (articles[0], 50), (articles[2], 50)));

        // Submitted, availability completed (reviewer can act)
        var submittedReady = Build(locations[2], RequestPriority.Normal, "john", now.AddMinutes(-60),
            (articles[5], 10), (articles[6], 25));
        submittedReady.Status = RequestStatus.Submitted;
        submittedReady.SubmittedAt = now.AddMinutes(-59);
        submittedReady.StockAvailabilityCheckStatus = StockAvailabilityCheckStatus.Completed;
        foreach (var i in submittedReady.Items) i.AvailableQuantity = i.RequestedQuantity;
        list.Add(submittedReady);

        // Approved, awaiting fulfillment
        var approved = Build(locations[3], RequestPriority.Low, "anna", now.AddHours(-3),
            (articles[9], 200), (articles[11], 5));
        approved.Status = RequestStatus.Approved;
        approved.SubmittedAt = now.AddHours(-3).AddMinutes(1);
        approved.ApprovedAt = now.AddHours(-2);
        approved.ReviewedBy = "jason";
        approved.StockAvailabilityCheckStatus = StockAvailabilityCheckStatus.Completed;
        foreach (var i in approved.Items) i.AvailableQuantity = i.RequestedQuantity;
        list.Add(approved);

        // Rejected (with reason)
        var rejected = Build(locations[0], RequestPriority.Urgent, "john", now.AddHours(-5),
            (articles[8], 1));
        rejected.Status = RequestStatus.Rejected;
        rejected.SubmittedAt = now.AddHours(-5).AddMinutes(1);
        rejected.RejectedAt = now.AddHours(-4);
        rejected.ReviewedBy = "jason";
        rejected.RejectionReason = "Insufficient justification for urgent priority.";
        rejected.StockAvailabilityCheckStatus = StockAvailabilityCheckStatus.Completed;
        foreach (var i in rejected.Items) i.AvailableQuantity = i.RequestedQuantity;
        list.Add(rejected);

        // Fulfilled (closed)
        var fulfilled = Build(locations[4], RequestPriority.Normal, "anna", now.AddDays(-1),
            (articles[7], 100));
        fulfilled.Status = RequestStatus.Fulfilled;
        fulfilled.SubmittedAt = now.AddDays(-1).AddMinutes(1);
        fulfilled.ApprovedAt = now.AddDays(-1).AddHours(1);
        fulfilled.FulfilledAt = now.AddDays(-1).AddHours(2);
        fulfilled.ReviewedBy = "jason";
        fulfilled.StockAvailabilityCheckStatus = StockAvailabilityCheckStatus.Completed;
        foreach (var i in fulfilled.Items)
        {
            i.AvailableQuantity = i.RequestedQuantity;
            i.FulfilledQuantity = i.RequestedQuantity;
        }
        list.Add(fulfilled);

        // Extra rows so the list view has visible variety
        list.Add(Build(locations[1], RequestPriority.Low, "john", now.AddMinutes(-10),
            (articles[10], 4)));

        var secondSubmitted = Build(locations[2], RequestPriority.Urgent, "anna", now.AddMinutes(-90),
            (articles[0], 25), (articles[2], 25));
        secondSubmitted.Status = RequestStatus.Submitted;
        secondSubmitted.SubmittedAt = now.AddMinutes(-89);
        secondSubmitted.StockAvailabilityCheckStatus = StockAvailabilityCheckStatus.Completed;
        foreach (var i in secondSubmitted.Items) i.AvailableQuantity = i.RequestedQuantity;
        list.Add(secondSubmitted);

        return list;
    }

    private static ReplenishmentRequest Build(
        StockLocation location,
        RequestPriority priority,
        string createdBy,
        DateTimeOffset createdAt,
        params (Article article, int quantity)[] items)
    {
        var requestId = Guid.NewGuid();
        var requestNumber = $"REP-{Guid.NewGuid().ToString("N").AsSpan(0, 8).ToString().ToUpperInvariant()}";

        return new ReplenishmentRequest
        {
            Id = requestId,
            RequestNumber = requestNumber,
            StockLocationId = location.Id,
            Priority = priority,
            Status = RequestStatus.Draft,
            StockAvailabilityCheckStatus = StockAvailabilityCheckStatus.NotStarted,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            Items = items.Select(i => new ReplenishmentRequestItem
            {
                Id = Guid.NewGuid(),
                RequestId = requestId,
                ArticleId = i.article.Id,
                RequestedQuantity = i.quantity
            }).ToList()
        };
    }
}
