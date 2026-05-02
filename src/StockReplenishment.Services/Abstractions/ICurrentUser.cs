using StockReplenishment.Contracts.Enums;

namespace StockReplenishment.Services.Abstractions;

/// <summary>
/// Abstraction over the caller identity. In this assignment it is populated from request headers
/// (<c>X-User</c>, <c>X-User-Role</c>) — in a real system this would be backed by ClaimsPrincipal.
/// </summary>
public interface ICurrentUser
{
    string UserName { get; }
    UserRole Role { get; }
    bool IsAuthenticated { get; }
}
