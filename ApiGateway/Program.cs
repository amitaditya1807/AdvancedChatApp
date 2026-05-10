using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

// 🔥 IMPORTANT: Render dynamic port binding
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
/*builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
*/
// 🔥 Hardcode localhost port
builder.WebHost.UseUrls("https://localhost:7000");

// CORS (safe for frontend apps)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// YARP reverse proxy
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors("AllowAll");

app.MapGet("/", () => "Gateway running 🚀");

app.MapReverseProxy();

app.Run();