using Microsoft.AspNetCore.Authentication.JwtBearer;
using ShellSpecter.Specter.Gpu;
using ShellSpecter.Specter.Hubs;
using ShellSpecter.Specter.Security;
using ShellSpecter.Specter.Services;
using ShellSpecter.Shared;

var builder = WebApplication.CreateBuilder(args);

// JWT Configuration
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "ShellSpecter-Default-Secret-Change-Me-In-Production-2024!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = JwtHelper.GetValidationParameters(jwtSecret);

        // Allow SignalR to receive the token via query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();

// CORS — allow the Seer dashboard origin
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:5051", "https://localhost:5051"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Register services
builder.Services.AddSingleton<TelemetryBroadcaster>();
builder.Services.AddSingleton<GpuCollector>();

// Use real or mock collector based on platform
if (OperatingSystem.IsLinux())
{
    builder.Services.AddHostedService<TelemetryCollector>();
}
else
{
    builder.Services.AddHostedService<MockTelemetryCollector>();
}

// Kestrel configuration
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5050);
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Serve the Seer Blazor WASM app (when deployed together)
app.UseStaticFiles();

// Auth endpoint
app.MapPost("/api/auth/login", (LoginRequest request) =>
{
    // On Linux, validate via PAM; on other platforms, accept any non-empty credentials for demo
    bool isAuthenticated;
    if (OperatingSystem.IsLinux())
    {
        isAuthenticated = PamAuth.Authenticate(request.Username, request.Password);
    }
    else
    {
        // Demo mode: accept any login with non-empty credentials
        isAuthenticated = !string.IsNullOrEmpty(request.Username) && !string.IsNullOrEmpty(request.Password);
    }

    if (!isAuthenticated)
    {
        return Results.Unauthorized();
    }

    var token = JwtHelper.GenerateToken(request.Username, jwtSecret);
    var expiry = DateTime.UtcNow.AddMinutes(480);

    return Results.Ok(new LoginResponse { Token = token, Expiry = expiry });
}).AllowAnonymous();

// Health check
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", host = Environment.MachineName }))
    .AllowAnonymous();

// SignalR hub
app.MapHub<TelemetryHub>("/hub/telemetry");

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Run();
