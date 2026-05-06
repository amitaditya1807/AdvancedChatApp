using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
// 🔥 ADD THIS LINE (fix)
builder.Configuration.AddEnvironmentVariables();

// 🔐 Read config
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

// 🔍 Debug logs
Console.WriteLine("==== CONFIG DEBUG ====");
Console.WriteLine("JWT Key: " + (jwtKey ?? "NULL"));
Console.WriteLine("Google ClientId: " + (googleClientId ?? "NULL"));
Console.WriteLine("Google Secret: " + (googleClientSecret ?? "NULL"));
Console.WriteLine("======================");

// 🔐 Authentication setup
var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
});

authBuilder.AddCookie();

// ✅ Add Google ONLY if env exists (prevents crash)
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
        options.SaveTokens = true;
    });

    builder.Services.PostConfigure<AuthenticationOptions>(options =>
    {
        options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
    });
}
else
{
    Console.WriteLine("⚠️ Google Auth NOT configured (missing env vars)");
}

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// ✅ Health check
app.MapGet("/", () => "Auth Service Running 🚀");

// 🔐 Google Login
app.MapGet("/google/login", async (HttpContext context) =>
{
    if (string.IsNullOrEmpty(googleClientId))
        return Results.BadRequest("Google auth not configured on server");

    await context.ChallengeAsync(GoogleDefaults.AuthenticationScheme,
        new AuthenticationProperties
        {
            RedirectUri = "/success"
        });

    return Results.Empty; // IMPORTANT
});

// ✅ Success → Generate JWT
app.MapGet("/success", (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        if (string.IsNullOrEmpty(jwtKey))
            return Results.BadRequest("JWT Key missing");

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

    return Results.Unauthorized();
});

app.Run();