namespace StockReplenishment.Web.Services;

/// <summary>
/// Per-circuit holder of the currently selected demo user. Components subscribe to <see cref="Changed"/>
/// to re-render when the user switches, and <see cref="ApiClient"/> reads <see cref="Current"/> when
/// stamping authentication headers.
/// </summary>
public sealed class UserContext
{
    public WebUser Current { get; private set; } = WebUsers.Default;

    public event Action? Changed;

    public void SetUser(WebUser user)
    {
        if (Current == user) return;
        Current = user;
        Changed?.Invoke();
    }
}
