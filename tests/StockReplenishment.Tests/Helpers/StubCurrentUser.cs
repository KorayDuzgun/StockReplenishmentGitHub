using StockReplenishment.Services.Abstractions;
using StockReplenishment.Contracts.Enums;

namespace StockReplenishment.Tests.Helpers;

/// <summary>Mutable <see cref="ICurrentUser"/> stub used to switch role between assertions.</summary>
internal sealed class StubCurrentUser : ICurrentUser
{
    public string UserName { get; set; } = "tester";
    public UserRole Role { get; set; } = UserRole.Worker;
    public bool IsAuthenticated { get; set; } = true;
}
