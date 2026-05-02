using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockReplenishment.Data.Persistence;
using StockReplenishment.Data.Repositories;

namespace StockReplenishment.Services;

/// <summary>Single composition entry point for everything the Services + Data layers own.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddStockReplenishmentRepositories(
        this IServiceCollection services,
        string inMemoryDatabaseName = "StockReplenishment")
    {
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(inMemoryDatabaseName));

        services.AddScoped<IArticleRepository, ArticleRepository>();
        services.AddScoped<IStockLocationRepository, StockLocationRepository>();
        services.AddScoped<IReplenishmentRequestRepository, ReplenishmentRequestRepository>();

        return services;
    }
}
