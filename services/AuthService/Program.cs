using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Read config
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

// 🔍 DEBUG LOGS (important)
Console.WriteLine("==== CONFIG DEBUG ====");
Console.WriteLine("JWT Key: " + (jwtKey ?? "NULL"));
Console.WriteLine("Google ClientId: " + (googleClientId ?? "NULL"));
Console.WriteLine("Google Secret: " + (googleClientSecret ?? "NULL"));
Console.WriteLine("======================");

// ✅ Always add auth (NO CRASH)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(options =>
{
    options.ClientId = googleClientId ?? "";
    options.ClientSecret = googleClientSecret ?? "";
    options.CallbackPath = "/signin-google";
    options.SaveTokens = true;
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Health check
app.MapGet("/", () => "Auth Service Running 🚀");

// Login
app.MapGet("/google/login", async (HttpContext context) =>
{
    if (string.IsNullOrEmpty(googleClientId))
        return Results.BadRequest("Google ClientId missing in env");

    await context.ChallengeAsync(GoogleDefaults.AuthenticationScheme,
        new AuthenticationProperties
        {
            RedirectUri = "/success"
        });
});

// Success
app.MapGet("/success", (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        if (string.IsNullOrEmpty(jwtKey))
            return Results.BadRequest("JWT Key missing in env");

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = context.User.FindFirst(ClaimTypes.Email)?.Value;
        var name = context.User.FindFirst(ClaimTypes.Name)?.Value;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId ?? ""),
            new Claim(ClaimTypes.Email, email ?? ""),
            new Claim(ClaimTypes.Name, name ?? ""),
            new Claim("provider", "google")
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer ?? "AuthService",
            audience: jwtAudience ?? "AllServices",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        return Results.Json(new
        {
            token = jwt,
            user = new { userId, email, name }
        });
    }

    return Results.Unauthorized();
});

app.Run();