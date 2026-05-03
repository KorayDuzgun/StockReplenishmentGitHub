using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StockReplenishment.Services.Abstractions;
using StockReplenishment.Data.Entities;
using StockReplenishment.Contracts.Enums;
using StockReplenishment.Data.Persistence;
using StockReplenishment.Services.Workflow;

namespace StockReplenishment.Tests.Workflow;

/// <summary>
/// Integration-style tests for the background worker. We wire a real DI scope but substitute the
/// external service so we can deterministically exercise both the happy path and the failure path.
/// </summary>
[TestFixture]
public class AvailabilityCheckBackgroundServiceTests
{
    [Test]
    public async Task ProcessAsync_HappyPath_MarksCompletedAndStoresAvailableQuantities()
    {
        var fixture = BuildFixture();

        fixture.Checker.CheckAsync(Arg.Any<ReplenishmentRequest>(), Arg.Any<CancellationToken>())
                       .Returns(call =>
                       {
                           var req = call.Arg<ReplenishmentRequest>();
                           return Task.FromResult<IReadOnlyDictionary<Guid, int>>(
                               req.Items.ToDictionary(i => i.Id, i => i.RequestedQuantity));
                       });

        await RunWorkerOnceAsync(fixture);

        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Requests.Include(r => r.Items).SingleAsync(r => r.Id == fixture.RequestId);

        Assert.That(saved.StockAvailabilityCheckStatus, Is.EqualTo(StockAvailabilityCheckStatus.Completed));
        Assert.That(saved.Items.All(i => i.AvailableQuantity == i.RequestedQuantity), Is.True);
    }

    [Test]
    public async Task ProcessAsync_WhenCheckerThrows_MarksFailed()
    {
        var fixture = BuildFixture();

        fixture.Checker.CheckAsync(Arg.Any<ReplenishmentRequest>(), Arg.Any<CancellationToken>())
                       .Returns<Task<IReadOnlyDictionary<Guid, int>>>(_ => throw new InvalidOperationException("boom"));

        await RunWorkerOnceAsync(fixture);

        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Requests.SingleAsync(r => r.Id == fixture.RequestId);

        Assert.That(saved.StockAvailabilityCheckStatus, Is.EqualTo(StockAvailabilityCheckStatus.Failed));
    }

    private static WorkerFixture BuildFixture()
    {
        var dbName = Guid.NewGuid().ToString();
        var checker = Substitute.For<IStockAvailabilityChecker>();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped(_ => checker);
        services.AddSingleton(_ => Channel.CreateUnbounded<Guid>());

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var locationId = Guid.NewGuid();
        var articleId = Guid.NewGuid();
        db.StockLocations.Add(new StockLocation { Id = locationId, Code = "LINE-A", Name = "Assembly Line A" });
        db.Articles.Add(new Article { Id = articleId, ArticleNumber = "BLT-M8-25", Description = "Bolt", Unit = "PCS" });

        var requestId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new ReplenishmentRequest
        {
            Id = requestId,
            RequestNumber = "REP-WORKER01",
            StockLocationId = locationId,
            Priority = RequestPriority.Normal,
            Status = RequestStatus.Submitted,
            StockAvailabilityCheckStatus = StockAvailabilityCheckStatus.NotStarted,
            CreatedBy = "john",
            CreatedAt = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero),
            SubmittedAt = new DateTimeOffset(2026, 5, 1, 8, 1, 0, TimeSpan.Zero),
            Items = [new ReplenishmentRequestItem
            {
                Id = itemId,
                RequestId = requestId,
                ArticleId = articleId,
                RequestedQuantity = 4
            }]
        };
        db.Requests.Add(request);
        db.SaveChanges();

        return new WorkerFixture(provider, checker, requestId);
    }

    private static async Task RunWorkerOnceAsync(WorkerFixture fixture)
    {
        var queue = fixture.Provider.GetRequiredService<Channel<Guid>>();
        await queue.Writer.WriteAsync(fixture.RequestId, CancellationToken.None);

        var worker = new AvailabilityCheckBackgroundService(
            queue,
            fixture.Provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AvailabilityCheckBackgroundService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var run = worker.StartAsync(cts.Token);

        // Poll for completion to keep the test fast (instead of sleeping a fixed duration).
        await using (var scope = fixture.Provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            while (!cts.IsCancellationRequested)
            {
                var current = await db.Requests.AsNoTracking().SingleAsync(r => r.Id == fixture.RequestId);
                if (current.StockAvailabilityCheckStatus is StockAvailabilityCheckStatus.Completed
                    or StockAvailabilityCheckStatus.Failed)
                    break;
                await Task.Delay(50, cts.Token);
            }
        }

        await worker.StopAsync(CancellationToken.None);
        await run;
    }

    private sealed record WorkerFixture(ServiceProvider Provider, IStockAvailabilityChecker Checker, Guid RequestId)
        : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }
}
