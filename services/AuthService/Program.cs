using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 🔐 Read configuration (Render ENV or local)
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

// 🔥 Debug logs (Render logs will show this)
Console.WriteLine("JWT Key exists: " + (jwtKey != null));
Console.WriteLine("Google ClientId: " + googleClientId);
Console.WriteLine("Google Secret exists: " + (googleClientSecret != null));

// ❌ Fail fast if config missing
if (string.IsNullOrEmpty(jwtKey))
    throw new Exception("JWT Key missing!");

if (string.IsNullOrEmpty(jwtIssuer))
    throw new Exception("JWT Issuer missing!");

if (string.IsNullOrEmpty(jwtAudience))
    throw new Exception("JWT Audience missing!");

if (string.IsNullOrEmpty(googleClientId) || string.IsNullOrEmpty(googleClientSecret))
    throw new Exception("Google OAuth config missing!");

// 🔐 Authentication setup
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(options =>
{
    options.ClientId = googleClientId;
    options.ClientSecret = googleClientSecret;
    options.CallbackPath = "/signin-google";
    options.SaveTokens = true;
});

// 🔐 Authorization
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// ✅ Health check
app.MapGet("/", () => "Auth Service Running 🚀");

// 🔐 Google Login
app.MapGet("/google/login", async (HttpContext context) =>
{
    await context.ChallengeAsync(GoogleDefaults.AuthenticationScheme,
        new AuthenticationProperties
        {
            RedirectUri = "/success"
        });
});

// ✅ Success → Generate JWT
app.MapGet("/success", (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
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
            user = new
            {
                userId,
                email,
                name
            }
        });
    }

    return Results.Unauthorized();
});

app.Run();