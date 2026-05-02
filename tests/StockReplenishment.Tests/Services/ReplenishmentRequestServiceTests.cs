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

    /// <summary>
    /// Approving a request that has already left the Submitted state must be rejected — guards
    /// against re-running side-effects (notifications, audit, fulfilment) when a stale UI button
    /// is clicked or two reviewers act on the same request concurrently.
    /// </summary>
    [TestCase(RequestStatus.Approved)]
    [TestCase(RequestStatus.Rejected)]
    [TestCase(RequestStatus.Fulfilled)]
    public async Task Approve_OnTerminalRequest_ReturnsConflict(RequestStatus terminalStatus)
    {
        _user.Role = UserRole.Worker;
        var draft = await CreateDraftAsync();
        await _service.SubmitAsync(draft.Id, CancellationToken.None);

        _user.Role = UserRole.Reviewer;
        await DriveToTerminalAsync(draft.Id, terminalStatus);

        var ex = Assert.ThrowsAsync<BusinessException>(() => _service.ApproveAsync(draft.Id, CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    /// <summary>
    /// Two fulfilment entries for the same line item must fail-fast rather than silently overwriting
    /// each other in the dictionary; this protects the auditability of the fulfilled quantity.
    /// </summary>
    [Test]
    public async Task Fulfill_WithDuplicateItemId_ReturnsBadRequest()
    {
        _user.Role = UserRole.Worker;
        var draft = await CreateDraftWithTwoItemsAsync();
        await _service.SubmitAsync(draft.Id, CancellationToken.None);

        _user.Role = UserRole.Reviewer;
        var approved = await _service.ApproveAsync(draft.Id, CancellationToken.None);

        var firstItemId = approved.Items[0].Id;
        var fulfillment = new FulfillRequestDto
        {
            Items =
            [
                new FulfillItemDto { ItemId = firstItemId, FulfilledQuantity = 1 },
                new FulfillItemDto { ItemId = firstItemId, FulfilledQuantity = 2 }
            ]
        };

        var ex = Assert.ThrowsAsync<BusinessException>(() => _service.FulfillAsync(draft.Id, fulfillment, CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    /// <summary>
    /// Every line on the request must have an explicit fulfilled quantity. Omitting one surfaces
    /// as 400 so the caller can fix the payload, instead of silently leaving the line un-fulfilled.
    /// </summary>
    [Test]
    public async Task Fulfill_WithMissingItemForLine_ReturnsBadRequest()
    {
        _user.Role = UserRole.Worker;
        var draft = await CreateDraftWithTwoItemsAsync();
        await _service.SubmitAsync(draft.Id, CancellationToken.None);

        _user.Role = UserRole.Reviewer;
        var approved = await _service.ApproveAsync(draft.Id, CancellationToken.None);

        // Second item intentionally omitted from the payload.
        var fulfillment = new FulfillRequestDto
        {
            Items = [new FulfillItemDto { ItemId = approved.Items[0].Id, FulfilledQuantity = 1 }]
        };

        var ex = Assert.ThrowsAsync<BusinessException>(() => _service.FulfillAsync(draft.Id, fulfillment, CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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

    /// <summary>
    /// A worker must not be able to act on another worker's draft. Role alone is insufficient —
    /// ownership must be enforced on write operations (read-side row-level security alone leaves
    /// a worker free to submit/update someone else's request when they know the id).
    /// </summary>
    [Test]
    public async Task Submit_OnAnotherWorkersDraft_IsForbidden()
    {
        _user.Role = UserRole.Worker;

        _user.UserName = "ali";
        var aliDraft = await CreateDraftAsync();

        _user.UserName = "ayse";

        var ex = Assert.ThrowsAsync<BusinessException>(() => _service.SubmitAsync(aliDraft.Id, CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    /// <summary>
    /// Review-only actions (approve / reject / fulfill) must be blocked for the Worker role —
    /// the inverse direction of <see cref="CreateDraft_AsReviewer_ReturnsForbidden"/>. Parameterised
    /// because <c>RequireRole</c> runs before any state check, so the same Submitted fixture
    /// covers all three actions.
    /// </summary>
    [TestCase("approve")]
    [TestCase("reject")]
    [TestCase("fulfill")]
    public async Task ReviewActions_AsWorker_ReturnForbidden(string action)
    {
        _user.Role = UserRole.Worker;
        var draft = await CreateDraftAsync();
        await _service.SubmitAsync(draft.Id, CancellationToken.None);

        Func<Task> act = action switch
        {
            "approve" => () => _service.ApproveAsync(draft.Id, CancellationToken.None),
            "reject"  => () => _service.RejectAsync(draft.Id, new RejectRequestDto { Reason = "x" }, CancellationToken.None),
            "fulfill" => () => _service.FulfillAsync(draft.Id, new FulfillRequestDto { Items = [] }, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

        var ex = Assert.ThrowsAsync<BusinessException>(() => act());
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

    /// <summary>Two-item draft used by fulfilment tests that need duplicate / missing item id coverage.</summary>
    private async Task<ReplenishmentRequestDto> CreateDraftWithTwoItemsAsync() =>
        await _service.CreateDraftAsync(new CreateRequestDto
        {
            StockLocationId = TestDb.LocationId,
            Priority = RequestPriority.Normal,
            Items =
            [
                new RequestItemInputDto { ArticleId = TestDb.Article1Id, RequestedQuantity = 3 },
                new RequestItemInputDto { ArticleId = TestDb.Article2Id, RequestedQuantity = 5 }
            ]
        }, CancellationToken.None);

    /// <summary>Drives a Submitted request to the requested terminal status using the public service API.</summary>
    private async Task DriveToTerminalAsync(Guid requestId, RequestStatus terminal)
    {
        switch (terminal)
        {
            case RequestStatus.Approved:
                await _service.ApproveAsync(requestId, CancellationToken.None);
                break;
            case RequestStatus.Rejected:
                await _service.RejectAsync(requestId, new RejectRequestDto { Reason = "test reason" }, CancellationToken.None);
                break;
            case RequestStatus.Fulfilled:
                var approved = await _service.ApproveAsync(requestId, CancellationToken.None);
                var dto = new FulfillRequestDto
                {
                    Items = approved.Items
                        .Select(i => new FulfillItemDto { ItemId = i.Id, FulfilledQuantity = i.RequestedQuantity })
                        .ToList()
                };
                await _service.FulfillAsync(requestId, dto, CancellationToken.None);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(terminal), terminal, "Not a terminal status.");
        }
    }
}
