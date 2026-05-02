using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockReplenishment.Data.Persistence;
using StockReplenishment.Data.Repositories;
using StockReplenishment.Services.Abstractions;

namespace StockReplenishment.Services;

/// <summary>Single composition entry point for everything the Services + Data layers own.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddStockReplenishment(
        this IServiceCollection services)
    {
        services.AddScoped<IReplenishmentRequestService, ReplenishmentRequestService>();

        return services;
    }
}
