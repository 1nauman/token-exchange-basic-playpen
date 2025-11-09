using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // The service needs the public key to verify the token's signature.
        var publicKeyPem = File.ReadAllText("/app/public_key.pem");
        var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false, // Not needed for this simple case
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "my-api-gateway", // It must trust our API Gateway as the issuer
            IssuerSigningKey = new RsaSecurityKey(rsa)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Product API is running!");

var products = new List<Product>
{
    new(1, "Photon Laptop", "High-performance laptop for developers.", 1200),
    new(2, "Quantum Mouse", "Ergonomic wireless mouse.", 75),
    new(3, "Singularity Keyboard", "Mechanical keyboard with RGB.", 150)
};

app.MapGet("/api/products/{id}", async (int id, HttpContext context, IHttpClientFactory clientFactory) =>
    {
        var userName = context.User.FindFirstValue("preferred_username") ?? "Unknown User";
        var hostName = System.Net.Dns.GetHostName();
        Console.WriteLine($"---> Product request handled by container '{hostName}' for user: '{userName}'");

        var product = products.FirstOrDefault(p => p.Id == id);
        if (product is null)
        {
            return Results.NotFound();
        }

        // --- AGGREGATION LOGIC STARTS HERE ---
        var httpClient = clientFactory.CreateClient();
        var stockCount = 0; // Default to 0 if inventory service fails

        try
        {
            // This is the service-to-service call. It uses the Docker service name.
            var inventoryResponse = await httpClient.GetAsync($"http://inventory-api:8080/inventory/{id}");
            if (inventoryResponse.IsSuccessStatusCode)
            {
                var inventoryItem = await inventoryResponse.Content.ReadFromJsonAsync<InventoryItem>();
                stockCount = inventoryItem?.StockCount ?? 0;
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't fail the request.
            Console.WriteLine($"---> Error calling inventory-api: {ex.Message}");
        }

        // Combine the data into a new response model
        var productDetail = new ProductDetail(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            stockCount
        );

        return Results.Ok(productDetail);
    })
    .RequireAuthorization();

app.Run();

record Product(int Id, string Name, string Description, decimal Price);

record InventoryItem(int ProductId, int StockCount);

record ProductDetail(int Id, string Name, string Description, decimal Price, int StockCount);