using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

// 🌍 Load ENV
builder.Configuration.AddEnvironmentVariables();

// 🌐 CORS (for frontend later)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// 🚀 YARP Reverse Proxy
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors("AllowAll");

// 🟢 Health check
app.MapGet("/", () => "Gateway running 🚀");

// 🔁 Proxy
app.MapReverseProxy();

app.Run();