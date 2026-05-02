using Microsoft.Extensions.DependencyInjection;
using StockReplenishment.Data.Persistence;
using StockReplenishment.Services;
using StockReplenishment.Services.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    // Serialize enums as strings on the wire so payloads stay self-describing
    // and UI clients don't have to mirror numeric values.
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHttpContextAccessor();

builder.Services.AddStockReplenishment();

// Permissive CORS keeps the Blazor Web project's HttpClient calls simple in dev.
// Production would tighten this to specific origins via configuration.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseSeeder.SeedAsync(db);
}

app.Run();

/// <summary>Marker partial used by integration tests via <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
