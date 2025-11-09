using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var publicKeyPem = File.ReadAllText("/app/public_key.pem");
        var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "my-api-gateway",
            IssuerSigningKey = new RsaSecurityKey(rsa)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "BFF API is running!");

app.MapGet("/api/products/{id}", async (int id, HttpContext context, IHttpClientFactory clientFactory) =>
{
    var userName = context.User.FindFirstValue("preferred_username") ?? "Unknown User";
    Console.WriteLine($"---> BFF: Aggregation started for user '{userName}'");

    var httpClient = clientFactory.CreateClient();

    var authorizationHeader = context.Request.Headers["Authorization"].ToString();

    if (!string.IsNullOrEmpty(authorizationHeader))
    {
        httpClient.DefaultRequestHeaders.Add("Authorization", authorizationHeader);
    }

    // Make parallel calls to the Product and Inventory APIs
    var productTask = httpClient.GetAsync($"http://product-api:8080/api/products/{id}");
    var inventoryTask = httpClient.GetAsync($"http://inventory-api:8080/inventory/{id}");

    await Task.WhenAll(productTask, inventoryTask);

    var productResponse = await productTask;
    var inventoryResponse = await inventoryTask;

    if (!productResponse.IsSuccessStatusCode)
    {
        return Results.Problem($"Product API returned an error. {productResponse.StatusCode}, statusCode: 502");
    }

    var product = await productResponse.Content.ReadFromJsonAsync<Product>();
    var stockCount = 0;

    if (inventoryResponse.IsSuccessStatusCode)
    {
        var inventoryItem = await inventoryResponse.Content.ReadFromJsonAsync<InventoryItem>();
        stockCount = inventoryItem?.StockCount ?? 0;
    }
    else
    {
        Console.WriteLine("---> BFF: Warning - Inventory API call failed. Defaulting stock to 0.");
    }

    var productDetail = new ProductDetail(
        product.Id, product.Name, product.Description, product.Price, stockCount
    );

    Console.WriteLine($"---> BFF: Aggregation complete for product {id}.");
    return Results.Ok(productDetail);
}).RequireAuthorization();

app.Run();

public record Product(int Id, string Name, string Description, decimal Price);

public record InventoryItem(int ProductId, int StockCount);

public record ProductDetail(int Id, string Name, string Description, decimal Price, int StockCount);