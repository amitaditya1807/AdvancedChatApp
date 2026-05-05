using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Read config
var jwtKey = builder.Configuration["Jwt:Key"];
var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];

// 🔐 JWT Authentication
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

        ValidIssuer = issuer,
        ValidAudience = audience,

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey!))
    };

    // 🔍 Debug logs
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("❌ Auth failed: " + context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("✅ Token validated");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// 🔹 Public endpoint
app.MapGet("/", () => "Chat Service Running");

// 🔹 Protected endpoint
app.MapGet("/secure", (HttpContext context) =>
{
    var user = context.User;

    var userId = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
    var name = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
    var email = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    var provider = user.Claims.FirstOrDefault(c => c.Type == "provider")?.Value;

    return Results.Json(new
    {
        message = "✅ You are authenticated!",
        user = new
        {
            userId,
            name,
            email,
            provider
        }
    });
})
.RequireAuthorization();

app.Run();