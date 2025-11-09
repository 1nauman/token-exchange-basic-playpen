var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Inventory API is running!");

// --- 1. Define a simple data model and some fake data ---
// In a real application, this would come from a database.
var inventory = new List<InventoryItem>
{
    new(1, 50), // 50 Laptops in stock
    new(2, 120), // 120 Mice in stock
    new(3, 0) // Keyboards are out of stock
};

// --- 2. Create the internal API endpoint ---
// This endpoint is simple and has no authorization.
app.MapGet("/inventory/{productId:int}", (int productId) =>
{
    Console.WriteLine($"---> Inventory check for Product ID: {productId}");

    var item = inventory.FirstOrDefault(i => i.ProductId == productId);

    // If no inventory record is found, we assume 0 stock.
    if (item is null)
    {
        return Results.Ok(new InventoryItem(productId, 0));
    }

    return Results.Ok(item);
});

app.Run();

record InventoryItem(int ProductId, int StockCount);