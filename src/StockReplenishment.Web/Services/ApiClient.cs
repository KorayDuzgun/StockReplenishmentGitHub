using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using StockReplenishment.Contracts.Dtos.Articles;
using StockReplenishment.Contracts.Dtos.Common;
using StockReplenishment.Contracts.Dtos.Locations;
using StockReplenishment.Contracts.Dtos.Requests;
using StockReplenishment.Contracts.Enums;

namespace StockReplenishment.Web.Services;

/// <summary>
/// Strongly-typed HTTP client for the Stock Replenishment API. Stamps the simulated-auth
/// headers from <see cref="UserContext"/> on every request and converts ProblemDetails
/// responses into <see cref="ApiException"/>s.
/// </summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly UserContext _user;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ApiClient(HttpClient http, UserContext user)
    {
        _http = http;
        _user = user;
    }

    // ----- Articles & Locations ------------------------------------------------------------------

    public Task<IReadOnlyList<ArticleDto>> ListArticlesAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<ArticleDto>>("api/articles", ct);

    public Task<IReadOnlyList<StockLocationDto>> ListLocationsAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<StockLocationDto>>("api/locations", ct);

    // ----- Requests ------------------------------------------------------------------------------

    public Task<PagedResult<RequestListItemDto>> ListRequestsAsync(
        RequestStatus? status, RequestPriority? priority, Guid? locationId, int page, int pageSize, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (status is not null) qs.Add($"status={status}");
        if (priority is not null) qs.Add($"priority={priority}");
        if (locationId is not null) qs.Add($"stockLocationId={locationId}");
        qs.Add($"page={page}");
        qs.Add($"pageSize={pageSize}");
        return GetAsync<PagedResult<RequestListItemDto>>($"api/requests?{string.Join('&', qs)}", ct);
    }

    public Task<ReplenishmentRequestDto> GetRequestAsync(Guid id, CancellationToken ct = default)
        => GetAsync<ReplenishmentRequestDto>($"api/requests/{id}", ct);

    public Task<ReplenishmentRequestDto> GetAvailabilityAsync(Guid id, CancellationToken ct = default)
        => GetAsync<ReplenishmentRequestDto>($"api/requests/{id}/availability", ct);

    public Task<ReplenishmentRequestDto> CreateRequestAsync(CreateRequestDto input, CancellationToken ct = default)
        => SendAsync<ReplenishmentRequestDto>(HttpMethod.Post, "api/requests", input, ct);

    public Task<ReplenishmentRequestDto> UpdateRequestAsync(Guid id, UpdateRequestDto input, CancellationToken ct = default)
        => SendAsync<ReplenishmentRequestDto>(HttpMethod.Put, $"api/requests/{id}", input, ct);

    public Task SubmitRequestAsync(Guid id, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, $"api/requests/{id}/submit", payload: null, ct);

    public Task<ReplenishmentRequestDto> ApproveAsync(Guid id, CancellationToken ct = default)
        => SendAsync<ReplenishmentRequestDto>(HttpMethod.Post, $"api/requests/{id}/approve", payload: null, ct);

    public Task<ReplenishmentRequestDto> RejectAsync(Guid id, RejectRequestDto input, CancellationToken ct = default)
        => SendAsync<ReplenishmentRequestDto>(HttpMethod.Post, $"api/requests/{id}/reject", input, ct);

    public Task<ReplenishmentRequestDto> FulfillAsync(Guid id, FulfillRequestDto input, CancellationToken ct = default)
        => SendAsync<ReplenishmentRequestDto>(HttpMethod.Post, $"api/requests/{id}/fulfill", input, ct);

    // ----- Internals -----------------------------------------------------------------------------

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var req = BuildRequest(HttpMethod.Get, url, payload: null);
        return await SendAndParseAsync<T>(req, ct);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? payload, CancellationToken ct)
    {
        using var req = BuildRequest(method, url, payload);
        return await SendAndParseAsync<T>(req, ct);
    }

    private async Task SendAsync(HttpMethod method, string url, object? payload, CancellationToken ct)
    {
        using var req = BuildRequest(method, url, payload);
        using var res = await _http.SendAsync(req, ct);
        await EnsureSuccessAsync(res, ct);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, object? payload)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-User", _user.Current.UserName);
        req.Headers.Add("X-User-Role", _user.Current.Role.ToString());
        if (payload is not null)
            req.Content = JsonContent.Create(payload, options: JsonOptions);
        return req;
    }

    private async Task<T> SendAndParseAsync<T>(HttpRequestMessage req, CancellationToken ct)
    {
        using var res = await _http.SendAsync(req, ct);
        await EnsureSuccessAsync(res, ct);
        var result = await res.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return result ?? throw new ApiException(res.StatusCode, "Empty response body.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;

        // Best-effort ProblemDetails parsing — fall back to status reason if the body is empty/malformed.
        ProblemDetails? problem = null;
        try { problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions, ct); }
        catch { /* ignored */ }

        var message = problem?.Detail ?? problem?.Title ?? res.ReasonPhrase ?? res.StatusCode.ToString();
        throw new ApiException(res.StatusCode, message);
    }
}
