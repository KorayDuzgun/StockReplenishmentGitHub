using System.Net;

namespace StockReplenishment.Services.Exceptions;

/// <summary>
/// All business-rule failures (validation, illegal state transitions, role denials) flow through
/// this single exception type. The <see cref="StatusCode"/> property tells the API middleware which
/// HTTP status to map to in the RFC 7807 ProblemDetails response.
/// </summary>
public class BusinessException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public BusinessException(string message, HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
