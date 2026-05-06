using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// =====================
// CONFIG
// =====================
builder.Configuration.AddEnvironmentVariables();

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

// =====================
// FORWARDED HEADERS (CRITICAL FOR RENDER)
// =====================
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// =====================
// AUTH
// =====================
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "auth_cookie";
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.IsEssential = true;
})
.AddGoogle(options =>
{
    options.ClientId = googleClientId!;
    options.ClientSecret = googleClientSecret!;
    options.CallbackPath = "/signin-google";

    options.SaveTokens = true;

    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    options.CorrelationCookie.IsEssential = true;
});

builder.Services.AddAuthorization();

var app = builder.Build();

// =====================
// MIDDLEWARE ORDER (CRITICAL)
// =====================

// 1. Fix proxy headers first
app.UseForwardedHeaders();

// 2. Force HTTPS scheme (Render proxy fix)
app.Use((context, next) =>
{
    context.Request.Scheme = "https";
    return next();
});

app.UseAuthentication();
app.UseAuthorization();

// =====================
// ROUTES
// =====================
app.MapGet("/", () => "Auth Service Running 🚀");

// =====================
// DEBUG (optional)
// =====================
app.MapGet("/debug/env", () =>
{
    return new
    {
        ClientId = Environment.GetEnvironmentVariable("Authentication__Google__ClientId"),
        Secret = Environment.GetEnvironmentVariable("Authentication__Google__ClientSecret"),
        Jwt = Environment.GetEnvironmentVariable("Jwt__Key")
    };
});

// =====================
// GOOGLE LOGIN
// =====================
app.MapGet("/auth/google/login", async (HttpContext context) =>
{
    if (string.IsNullOrEmpty(googleClientId))
        return Results.BadRequest("Google ClientId missing");

    await context.ChallengeAsync(GoogleDefaults.AuthenticationScheme,
        new AuthenticationProperties
        {
            RedirectUri = "/auth/success"
        });

    return Results.Empty;
});

// =====================
// SUCCESS CALLBACK (SAFE)
// =====================
app.MapGet("/auth/success", (HttpContext context) =>
{
    try
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return Results.BadRequest("User not authenticated after Google login");

        if (string.IsNullOrEmpty(jwtKey))
            return Results.BadRequest("JWT Key missing in environment variables");

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
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Auth failed",
            detail: ex.Message
        );
    }
});

app.Run();