using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

// ✅ YARP without custom handler (simplest working version)
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

app.MapGet("/", () => "Gateway working");

app.MapReverseProxy();

app.Run();