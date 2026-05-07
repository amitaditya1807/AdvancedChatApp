using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 🌍 Load ENV
builder.Configuration.AddEnvironmentVariables();

// 🔐 JWT Config
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer ?? "AuthService",
            ValidAudience = jwtAudience ?? "AllServices",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey!))
        };
    });

builder.Services.AddAuthorization();

// 🌐 CORS (important for frontend)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// 🟢 Health check
app.MapGet("/", () => "Chat Service Running 🚀");

// 🔒 Protected route
app.MapGet("/chat", (HttpContext context) =>
{
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    return Results.Json(new
    {
        message = $"Hello {userId}, Chat service working!",
        time = DateTime.UtcNow
    });
})
.RequireAuthorization();

// 🎬 Protected YouTube helper route
app.MapPost("/chat/youtube/prepare", (YouTubeDownloadRequest request, HttpContext context) =>
{
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var email = context.User.FindFirst(ClaimTypes.Email)?.Value;
    var rawUrl = request.Url?.Trim();

    if (string.IsNullOrWhiteSpace(rawUrl))
        return Results.BadRequest(new { message = "Please enter a YouTube URL." });

    if (!request.HasPermission)
    {
        return Results.BadRequest(new
        {
            message = "Please confirm that you own this video or have permission to download it."
        });
    }

    if (!TryGetYouTubeVideoId(rawUrl, out var videoId))
    {
        return Results.BadRequest(new
        {
            message = "Please enter a valid YouTube video URL.",
            supportedFormats = new[]
            {
                "https://www.youtube.com/watch?v=VIDEO_ID",
                "https://youtu.be/VIDEO_ID",
                "https://www.youtube.com/shorts/VIDEO_ID",
                "https://www.youtube.com/embed/VIDEO_ID"
            }
        });
    }

    var watchUrl = $"https://www.youtube.com/watch?v={videoId}";

    return Results.Json(new
    {
        title = "YouTube download request prepared",
        message = "Validated an authorized YouTube download request. Use this only for videos you own or have permission to download.",
        videoId,
        watchUrl,
        embedUrl = $"https://www.youtube.com/embed/{videoId}",
        requestedBy = new { userId, email },
        status = "Ready for an authorized downloader workflow",
        nextSteps = new[]
        {
            "Confirm the video is your own content or you have permission from the owner.",
            "For your own channel videos, YouTube Studio is the safest official download option.",
            "If you later add server-side downloading, keep this permission check and store an audit log for each request."
        },
        time = DateTime.UtcNow
    });
})
.RequireAuthorization();

app.Run();

static bool TryGetYouTubeVideoId(string url, out string videoId)
{
    videoId = string.Empty;

    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        return false;

    var host = uri.Host.ToLowerInvariant();
    var path = uri.AbsolutePath.Trim('/');

    if (host is "youtu.be" or "www.youtu.be")
    {
        videoId = path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return IsValidYouTubeVideoId(videoId);
    }

    if (host is not "youtube.com" and not "www.youtube.com" and not "m.youtube.com")
        return false;

    if (path.Equals("watch", StringComparison.OrdinalIgnoreCase))
    {
        videoId = GetQueryValue(uri.Query, "v") ?? string.Empty;
        return IsValidYouTubeVideoId(videoId);
    }

    var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (segments.Length >= 2 &&
        (segments[0].Equals("shorts", StringComparison.OrdinalIgnoreCase) ||
         segments[0].Equals("embed", StringComparison.OrdinalIgnoreCase)))
    {
        videoId = segments[1];
        return IsValidYouTubeVideoId(videoId);
    }

    return false;
}

static string? GetQueryValue(string queryString, string key)
{
    var query = queryString.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);

    foreach (var item in query)
    {
        var pair = item.Split('=', 2);
        var queryKey = Uri.UnescapeDataString(pair[0]);

        if (!queryKey.Equals(key, StringComparison.OrdinalIgnoreCase))
            continue;

        return pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
    }

    return null;
}

static bool IsValidYouTubeVideoId(string videoId)
{
    return videoId.Length == 11 &&
        videoId.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');
}

record YouTubeDownloadRequest(string? Url, bool HasPermission);