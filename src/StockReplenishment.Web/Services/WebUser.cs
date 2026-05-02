using StockReplenishment.Contracts.Enums;

namespace StockReplenishment.Web.Services;

/// <summary>One of the impersonation profiles offered by the user-selector dropdown.</summary>
public sealed record WebUser(string UserName, UserRole Role, string DisplayName);

/// <summary>Static catalogue of available demo users. Production would replace this with real auth.</summary>
public static class WebUsers
{
    public static readonly WebUser JohnWorker = new("john", UserRole.Worker, "John (Worker)");
    public static readonly WebUser AnnaWorker = new("anna", UserRole.Worker, "Anna (Worker)");
    public static readonly WebUser JasonReviewer = new("jason", UserRole.Reviewer, "Jason (Reviewer)");

    public static readonly IReadOnlyList<WebUser> All = [JohnWorker, AnnaWorker, JasonReviewer];
    public static WebUser Default => JohnWorker;
}
