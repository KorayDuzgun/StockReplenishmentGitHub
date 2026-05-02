using System.Net;

namespace StockReplenishment.Web.Services;

/// <summary>
/// Raised by <see cref="ApiClient"/> for non-success HTTP responses. Carries the parsed
/// <c>ProblemDetails</c> title/detail when available so the UI can show meaningful messages.
/// </summary>
public sealed class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public ApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
