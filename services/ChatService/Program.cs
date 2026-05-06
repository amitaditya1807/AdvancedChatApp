using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var key = "THIS_IS_MY_SUPER_SECURE_JWT_SECRET_KEY_2026";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = "AuthService",
        ValidAudience = "AllServices",

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Chat Service Running");

app.MapGet("/secure", (HttpContext context) =>
{
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    var email = context.User.FindFirst(ClaimTypes.Email)?.Value;

    var name = context.User.FindFirst(ClaimTypes.Name)?.Value;

    return Results.Json(new
    {
        message = "✅ You are authenticated!",
        user = new
        {
            userId,
            name,
            email
        }
    });
}).RequireAuthorization();

app.Run();