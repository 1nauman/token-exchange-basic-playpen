using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var privateKeyPem = await File.ReadAllTextAsync("private_key.pem");
var rsa = RSA.Create();
rsa.ImportFromPem(privateKeyPem.ToCharArray());
var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);

app.MapGet("/exchange/{*remainder}", (HttpContext context) =>
{
    // 1. Envoy forwards the original token as header
    if (!context.Request.Headers.TryGetValue("x-jwt-payload", out var jwtPayloadHeader))
    {
        return Results.Unauthorized();
    }

    try
    {
        // 2. The payload is Base64Url encoded. Decode it.
        var jsonPayload = Base64UrlEncoder.Decode(jwtPayloadHeader.First());
        var externalClaims = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonPayload);

        if (externalClaims == null)
        {
            return Results.BadRequest("Invalid payload.");
        }

        // 3. Create the claims for the new internal token.
        //    Only copy the claims you trust and need.
        var internalClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, externalClaims["sub"]?.ToString() ?? string.Empty),
            new("preferred_username", externalClaims["preferred_username"]?.ToString() ?? string.Empty)
            // Add any other claims you need, e.g., tenant_id, roles
        };

        // 4. Create and sign the new internal JWT
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = "my-api-gateway",
            Subject = new ClaimsIdentity(internalClaims),
            Expires = DateTime.UtcNow.AddMinutes(15), // Give it a short expiry
            SigningCredentials = signingCredentials
        };
        var internalToken = tokenHandler.CreateToken(tokenDescriptor);
        var internalTokenString = tokenHandler.WriteToken(internalToken);

        // 5. Return the new token in a response header for Envoy to use
        context.Response.Headers["x-internal-jwt"] = internalTokenString;
        // context.Response.Headers["Authorization"] = $"Bearer {internalTokenString}";
        return Results.Ok();
    }
    catch (Exception ex)
    {
        // Log the exception in a real application
        Console.WriteLine($"Error exchanging token: {ex.Message}");
        return Results.BadRequest("Error processing token.");
    }
});

app.Run();