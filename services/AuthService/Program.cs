using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 🔥 Load ENV (Render)
builder.Configuration.AddEnvironmentVariables();

// 🔐 Read config (USE ':' NOT '__')
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

// 🔍 Debug
Console.WriteLine("==== CONFIG DEBUG ====");
Console.WriteLine("JWT Key: " + (jwtKey ?? "NULL"));
Console.WriteLine("Google ClientId: " + (googleClientId ?? "NULL"));
Console.WriteLine("Google Secret: " + (googleClientSecret ?? "NULL"));
Console.WriteLine("======================");

// 🔐 AUTH CONFIG (PRODUCTION SAFE)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "auth_cookie";
    options.Cookie.SameSite = SameSiteMode.None;                 // ✅ REQUIRED
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;     // ✅ REQUIRED
})
.AddGoogle(options =>
{
    options.ClientId = googleClientId!;
    options.ClientSecret = googleClientSecret!;
    options.CallbackPath = "/signin-google";

    // 🔥 FIX CORRELATION ERROR
    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;

    options.SaveTokens = true;
});

builder.Services.AddAuthorization();

var app = builder.Build();

// 🔥 VERY IMPORTANT FOR RENDER (HTTPS via proxy)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});

// ❌ DO NOT USE THIS ON RENDER
// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// ✅ Health check
app.MapGet("/", () => "Auth Service Running 🚀");

// ✅ Debug ENV
app.MapGet("/debug/env", () =>
{
    return new
    {
        ClientId = Environment.GetEnvironmentVariable("Authentication__Google__ClientId"),
        Secret = Environment.GetEnvironmentVariable("Authentication__Google__ClientSecret"),
        Jwt = Environment.GetEnvironmentVariable("Jwt__Key")
    };
});

// =========================
// 🔐 AUTH ROUTES
// =========================

var authGroup = app.MapGroup("/auth");

// 🔐 Google Login
authGroup.MapGet("/google/login", async (HttpContext context) =>
{
    if (string.IsNullOrEmpty(googleClientId))
        return Results.BadRequest("Google auth not configured on server");

    await context.ChallengeAsync(GoogleDefaults.AuthenticationScheme,
        new AuthenticationProperties
        {
            RedirectUri = "/auth/success"
        });

    return Results.Empty;
});

// ✅ Success → Generate JWT
authGroup.MapGet("/success", (HttpContext context) =>
{
    try
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Results.BadRequest("User not authenticated after Google login");
        }

        var jwtKey = builder.Configuration["Jwt:Key"];
        var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AuthService";
        var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AllServices";

        if (string.IsNullOrEmpty(jwtKey))
        {
            return Results.BadRequest("Jwt:Key is missing in environment variables");
        }

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var email = context.User.FindFirst(ClaimTypes.Email)?.Value ?? "";
        var name = context.User.FindFirst(ClaimTypes.Name)?.Value ?? "";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, name),
            new Claim("provider", "google")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
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
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            error = "Auth failed safely (no 500 anymore)",
            message = ex.Message
        });
    }
});

app.Run();