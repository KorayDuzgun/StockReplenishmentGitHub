using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockReplenishment.Data.Persistence;
using StockReplenishment.Data.Repositories;
using StockReplenishment.Services.Abstractions;
using StockReplenishment.Services.Workflow;

namespace StockReplenishment.Services;

/// <summary>Single composition entry point for everything the Services + Data layers own.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddStockReplenishmentServices(
        this IServiceCollection services)
    {
        services.AddScoped<IReplenishmentRequestService, ReplenishmentRequestService>();
        // Bounded channel applies natural back-pressure if producers ever outpace the worker,
        // preferable to unbounded growth. Single-reader/multi-writer matches our usage.
        services.AddSingleton(_ => Channel.CreateBounded<Guid>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        }));
        
        services.AddScoped<IStockAvailabilityChecker, SimulatedStockAvailabilityChecker>();
        services.AddHostedService<AvailabilityCheckBackgroundService>();
        return services;
    }
}
