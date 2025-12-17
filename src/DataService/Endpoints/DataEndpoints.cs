using Microsoft.EntityFrameworkCore;
using SharedEntities;
using ZavaDatabaseInitialization;

namespace DataService.Endpoints;

public static class DataEndpoints
{
    public static void MapDataEndpoints(this IEndpointRouteBuilder routes)
    {
        var customerGroup = routes.MapGroup("/api/Customer");
        var toolGroup = routes.MapGroup("/api/Tool");
        var locationGroup = routes.MapGroup("/api/Location");

        // Customer endpoints
        customerGroup.MapGet("/", async (Context db) =>
        {
            var customers = await db.Customer.ToListAsync();
            return Results.Ok(customers);
        })
        .WithName("GetAllCustomers")
        .Produces<List<CustomerInformation>>(StatusCodes.Status200OK);

        customerGroup.MapGet("/{id}", async (string id, Context db) =>
        {
            var customer = await db.Customer.FindAsync(id);
            return customer is not null ? Results.Ok(customer) : Results.NotFound();
        })
        .WithName("GetCustomerById")
        .Produces<CustomerInformation>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // Tool endpoints
        toolGroup.MapGet("/", async (Context db) =>
        {
            var tools = await db.Tool.ToListAsync();
            return Results.Ok(tools);
        })
        .WithName("GetAllTools")
        .Produces<List<ToolRecommendation>>(StatusCodes.Status200OK);

        toolGroup.MapGet("/{sku}", async (string sku, Context db) =>
        {
            var tool = await db.Tool.FindAsync(sku);
            return tool is not null ? Results.Ok(tool) : Results.NotFound();
        })
        .WithName("GetToolBySku")
        .Produces<ToolRecommendation>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        toolGroup.MapGet("/available", async (Context db) =>
        {
            var tools = await db.Tool.Where(t => t.IsAvailable).ToListAsync();
            return Results.Ok(tools);
        })
        .WithName("GetAvailableTools")
        .Produces<List<ToolRecommendation>>(StatusCodes.Status200OK);

        // Location endpoints
        locationGroup.MapGet("/", async (Context db) =>
        {
            var locations = await db.Location.ToListAsync();
            return Results.Ok(locations);
        })
        .WithName("GetAllLocations")
        .Produces<List<StoreLocation>>(StatusCodes.Status200OK);

        locationGroup.MapGet("/search", async (string? query, Context db) =>
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                var allLocations = await db.Location.ToListAsync();
                return Results.Ok(allLocations);
            }

            var locations = await db.Location
                .Where(l => EF.Functions.Like(l.Section, $"%{query}%") ||
                           EF.Functions.Like(l.Description, $"%{query}%"))
                .ToListAsync();
            return Results.Ok(locations);
        })
        .WithName("SearchLocations")
        .Produces<List<StoreLocation>>(StatusCodes.Status200OK);
    }
}
