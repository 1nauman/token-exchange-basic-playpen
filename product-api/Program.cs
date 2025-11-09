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

app.MapGet("/api/products/{id}", (int id, HttpContext context) =>
    {
        var userName = context.User.FindFirstValue("preferred_username") ?? "Unknown User";
        var hostName = System.Net.Dns.GetHostName();
        Console.WriteLine($"---> Product request handled by container '{hostName}' for user: '{userName}'");

        var product = products.FirstOrDefault(p => p.Id == id);

        return product is not null ? Results.Ok(product) : Results.NotFound();
    })
    .RequireAuthorization();

app.Run();

record Product(int Id, string Name, string Description, decimal Price);