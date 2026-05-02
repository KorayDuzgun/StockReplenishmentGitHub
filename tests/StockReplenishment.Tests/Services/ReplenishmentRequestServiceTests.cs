using System.Net;
using System.Threading.Channels;
using StockReplenishment.Contracts.Dtos.Common;
using StockReplenishment.Contracts.Dtos.Requests;
using StockReplenishment.Contracts.Enums;
using StockReplenishment.Data.Persistence;
using StockReplenishment.Data.Repositories;
using StockReplenishment.Services;
using StockReplenishment.Services.Exceptions;
using StockReplenishment.Tests.Helpers;

namespace StockReplenishment.Tests.Services;

/// <summary>
/// End-to-end service tests covering authorization, state transitions, validation and the
/// queue side-effect on submit. Exercises the real DbContext (in-memory) + repositories so the
/// EF projections, includes and indexes used by production code are also touched.
/// </summary>
[TestFixture]
public class ReplenishmentRequestServiceTests
{
    private AppDbContext _db = default!;
    private Channel<Guid> _queue = default!;
    private StubCurrentUser _user = default!;
    private ReplenishmentRequestService _service = default!;

    [SetUp]
    public void SetUp()
    {
        _db = TestDb.CreateContext();
        _queue = Channel.CreateUnbounded<Guid>();
        _user = new StubCurrentUser();

        IReplenishmentRequestRepository requests = new ReplenishmentRequestRepository(_db);
        IStockLocationRepository locations = new StockLocationRepository(_db);
        IArticleRepository articles = new ArticleRepository(_db);

        _service = new ReplenishmentRequestService(_db, requests, locations, articles, _queue, _user);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    // ------------------------- happy path --------------------------------------------------------

    [Test]
    public async Task FullLifecycle_DraftSubmitApproveFulfill_TransitionsCleanly()
    {
        _user.Role = UserRole.Worker;
        _user.UserName = "ali";

        var draft = await CreateDraftAsync();
        Assert.That(draft.Status, Is.EqualTo(RequestStatus.Draft));
        Assert.That(draft.RequestNumber, Does.StartWith("REP-"));

        await _service.SubmitAsync(draft.Id, CancellationToken.None);

        // Worker is no longer the right role for review actions; switch identity.
        _user.Role = UserRole.Reviewer;
        _user.UserName = "mehmet";

        var approved = await _service.ApproveAsync(draft.Id, CancellationToken.None);
        Assert.That(approved.Status, Is.EqualTo(RequestStatus.Approved));
        Assert.That(approved.ReviewedBy, Is.EqualTo("mehmet"));

        var fulfillment = new FulfillRequestDto
        {
            Items = approved.Items.Select(i => new FulfillItemDto { ItemId = i.Id, FulfilledQuantity = i.RequestedQuantity }).ToList()
        };
        var fulfilled = await _service.FulfillAsync(draft.Id, fulfillment, CancellationToken.None);

        Assert.That(fulfilled.Status, Is.EqualTo(RequestStatus.Fulfilled));
        Assert.That(fulfilled.Items.All(i => i.FulfilledQuantity == i.RequestedQuantity), Is.True);
    }

    // ------------------------- state-transition guards -------------------------------------------

    [Test]
    public async Task Submit_OnAlreadySubmittedRequest_ReturnsConflict()
    {
        _user.Role = UserRole.Worker;
        var draft = await CreateDraftAsync();
        await _service.SubmitAsync(draft.Id, CancellationToken.None);

        var ex = Assert.ThrowsAsync<BusinessException>(() => _service.SubmitAsync(draft.Id, CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Submit_OnEmptyDraft_ReturnsUnprocessable()
    {
        _user.Role = UserRole.Worker;
        var draft = await _service.CreateDraftAsync(new CreateRequestDto
        {
            StockLocationId = TestDb.LocationId,
            Priority = RequestPriority.Normal,
            Items = []
        }, CancellationToken.None);

        var ex = Assert.ThrowsAsync<BusinessException>(() => _service.SubmitAsync(draft.Id, CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    [Test]
    public async Task Approve_OnNonSubmittedRequest_ReturnsConflict()
    {
        _user.Role = UserRole.Worker;
        var draft = await CreateDraftAsync();

        _user.Role = UserRole.Reviewer;
        var ex = Assert.ThrowsAsync<BusinessException>(() => _service.ApproveAsync(draft.Id, CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Reject_WithoutReason_ReturnsBadRequest()
    {
        _user.Role = UserRole.Worker;
        var draft = await CreateDraftAsync();
        await _service.SubmitAsync(draft.Id, CancellationToken.None);

        _user.Role = UserRole.Reviewer;
        var ex = Assert.ThrowsAsync<BusinessException>(() =>
            _service.RejectAsync(draft.Id, new RejectRequestDto { Reason = "  " }, CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Fulfill_WithQuantityExceedingRequested_ReturnsUnprocessable()
    {
        _user.Role = UserRole.Worker;
        var draft = await CreateDraftAsync();
        await _service.SubmitAsync(draft.Id, CancellationToken.None);

        _user.Role = UserRole.Reviewer;
        var approved = await _service.ApproveAsync(draft.Id, CancellationToken.None);

        var fulfillment = new FulfillRequestDto
        {
            Items = approved.Items.Select(i => new FulfillItemDto { ItemId = i.Id, FulfilledQuantity = i.RequestedQuantity + 1 }).ToList()
        };

        var ex = Assert.ThrowsAsync<BusinessException>(() => _service.FulfillAsync(draft.Id, fulfillment, CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    // ------------------------- authorization -----------------------------------------------------

    [Test]
    public void CreateDraft_AsReviewer_ReturnsForbidden()
    {
        _user.Role = UserRole.Reviewer;
        var ex = Assert.ThrowsAsync<BusinessException>(() => _service.CreateDraftAsync(new CreateRequestDto
        {
            StockLocationId = TestDb.LocationId,
            Priority = RequestPriority.Normal,
            Items = []
        }, CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    // ------------------------- queue side-effect -------------------------------------------------

    [Test]
    public async Task Submit_WritesRequestIdToAvailabilityQueue()
    {
        _user.Role = UserRole.Worker;
        var draft = await CreateDraftAsync();

        await _service.SubmitAsync(draft.Id, CancellationToken.None);

        Assert.That(_queue.Reader.TryRead(out var enqueuedId), Is.True);
        Assert.That(enqueuedId, Is.EqualTo(draft.Id));
    }

    // ------------------------- row-level security ------------------------------------------------

    [Test]
    public async Task ListAsync_AsWorker_OnlyReturnsTheirOwnRequests()
    {
        _user.Role = UserRole.Worker;

        _user.UserName = "ali";
        var aliRequest = await CreateDraftAsync();

        _user.UserName = "ayse";
        var ayseRequest = await CreateDraftAsync();

        var asAyse = await _service.ListAsync(new RequestQuery(), CancellationToken.None);

        Assert.That(asAyse.Items.Select(i => i.Id), Is.EquivalentTo(new[] { ayseRequest.Id }));
        Assert.That(asAyse.Items.Select(i => i.Id), Does.Not.Contain(aliRequest.Id));
    }

    [Test]
    public async Task ListAsync_AsReviewer_ReturnsAllRequests()
    {
        _user.Role = UserRole.Worker;

        _user.UserName = "ali";
        var aliRequest = await CreateDraftAsync();

        _user.UserName = "ayse";
        var ayseRequest = await CreateDraftAsync();

        _user.Role = UserRole.Reviewer;
        _user.UserName = "mehmet";
        var asReviewer = await _service.ListAsync(new RequestQuery(), CancellationToken.None);

        Assert.That(asReviewer.Items.Select(i => i.Id), Is.EquivalentTo(new[] { aliRequest.Id, ayseRequest.Id }));
    }

    [Test]
    public async Task ListAsync_AsWorker_IgnoresClientSuppliedCreatedByOverride()
    {
        _user.Role = UserRole.Worker;

        _user.UserName = "ali";
        await CreateDraftAsync();

        _user.UserName = "ayse";
        var ayseRequest = await CreateDraftAsync();

        // A malicious client tries to view Ali's data by spoofing the filter.
        var spoofed = await _service.ListAsync(new RequestQuery { CreatedBy = "ali" }, CancellationToken.None);

        Assert.That(spoofed.Items.Select(i => i.Id), Is.EquivalentTo(new[] { ayseRequest.Id }));
    }

    [Test]
    public async Task ListAsync_FiltersByStatus()
    {
        _user.Role = UserRole.Worker;
        var draft = await CreateDraftAsync();
        var submitted = await CreateDraftAsync();
        await _service.SubmitAsync(submitted.Id, CancellationToken.None);

        PagedResult<RequestListItemDto> page = await _service.ListAsync(
            new RequestQuery { Status = RequestStatus.Submitted }, CancellationToken.None);

        Assert.That(page.Items, Has.Count.EqualTo(1));
        Assert.That(page.Items[0].Id, Is.EqualTo(submitted.Id));
        Assert.That(page.Items[0].Id, Is.Not.EqualTo(draft.Id));
    }

    private async Task<ReplenishmentRequestDto> CreateDraftAsync() =>
        await _service.CreateDraftAsync(new CreateRequestDto
        {
            StockLocationId = TestDb.LocationId,
            Priority = RequestPriority.Normal,
            Items = [new RequestItemInputDto { ArticleId = TestDb.Article1Id, RequestedQuantity = 3 }]
        }, CancellationToken.None);
}
