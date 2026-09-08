var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/status", () => new {
    gateway = "ESP32-EdgeGateway",
    status = "Online",
    uptimeSeconds = Environment.TickCount64 / 1000,
    isHealthy = true
});
app.Run();
