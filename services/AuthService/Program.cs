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
var jwtExpiryMinutes = builder.Configuration.GetValue("Jwt:ExpiryMinutes", 60);

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var frontendRedirectUrl = builder.Configuration["Frontend:RedirectUrl"]
    ?? Environment.GetEnvironmentVariable("FRONTEND_REDIRECT_URL");

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

        var issuedAt = DateTime.UtcNow;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, name),
            new Claim("provider", "google"),
            new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(issuedAt).ToString(), ClaimValueTypes.Integer64)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer ?? "AuthService",
            audience: jwtAudience ?? "AllServices",
            claims: claims,
            notBefore: issuedAt,
            expires: issuedAt.AddMinutes(jwtExpiryMinutes),
            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        var (redirectUrl, redirectError) = CreateFrontendTokenRedirectUrl(frontendRedirectUrl, jwt);

        if (!string.IsNullOrEmpty(redirectError))
            return Results.BadRequest(redirectError);

        if (!string.IsNullOrEmpty(redirectUrl))
            return Results.Redirect(redirectUrl);

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

static (string? RedirectUrl, string? Error) CreateFrontendTokenRedirectUrl(string? frontendRedirectUrl, string jwt)
{
    if (string.IsNullOrWhiteSpace(frontendRedirectUrl))
        return (null, null);

    var trimmedUrl = frontendRedirectUrl.Trim();

    if (trimmedUrl.Contains("your-frontend", StringComparison.OrdinalIgnoreCase) ||
        trimmedUrl.Contains("YOUR-FRONTEND-SERVICE", StringComparison.OrdinalIgnoreCase))
    {
        return (null,
            "Frontend redirect URL is still using the placeholder value. " +
            "Set AuthService Frontend__RedirectUrl to the real Render Static Site URL, for example https://advanced-chat-frontend.onrender.com/.");
    }

    if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var frontendUri) ||
        (frontendUri.Scheme != Uri.UriSchemeHttps && frontendUri.Scheme != Uri.UriSchemeHttp))
    {
        return (null,
            "Frontend redirect URL must be an absolute http or https URL, for example https://advanced-chat-frontend.onrender.com/.");
    }

    var builder = new UriBuilder(frontendUri);
    var queryPrefix = string.IsNullOrWhiteSpace(builder.Query) ? string.Empty : builder.Query.TrimStart('?') + "&";
    builder.Query = queryPrefix + "token=" + Uri.EscapeDataString(jwt);

    return (builder.Uri.ToString(), null);
}