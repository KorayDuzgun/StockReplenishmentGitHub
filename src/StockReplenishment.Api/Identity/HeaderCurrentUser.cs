using Microsoft.AspNetCore.Http;
using StockReplenishment.Services.Abstractions;
using StockReplenishment.Contracts.Enums;

namespace StockReplenishment.Api.Identity;

/// <summary>
/// Reads the simulated identity from request headers (<c>X-User</c>, <c>X-User-Role</c>).
/// In a real system this would be replaced by an identity provider; the abstraction lets us swap
/// the implementation without touching domain or service code.
/// </summary>
internal sealed class HeaderCurrentUser : ICurrentUser
{
    public const string UserHeader = "X-User";
    public const string RoleHeader = "X-User-Role";

    public string UserName { get; }
    public UserRole Role { get; }
    public bool IsAuthenticated { get; }

    public HeaderCurrentUser(IHttpContextAccessor accessor)
    {
        var ctx = accessor.HttpContext;
        if (ctx is null)
        {
            UserName = string.Empty;
            Role = UserRole.Worker;
            IsAuthenticated = false;
            return;
        }

        var name = ctx.Request.Headers[UserHeader].ToString();
        var role = ctx.Request.Headers[RoleHeader].ToString();

        UserName = name;
        IsAuthenticated = !string.IsNullOrWhiteSpace(name) && Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsed);
        Role = IsAuthenticated && Enum.TryParse<UserRole>(role, ignoreCase: true, out var ok) ? ok : UserRole.Worker;
    }
}
