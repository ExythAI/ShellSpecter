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
app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});

// Auth endpoint
app.MapPost("/api/auth/login", (LoginRequest request, ILogger<Program> logger) =>
{
    if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
    {
        return Results.Json(new { error = "Username and password required" }, statusCode: 401);
    }

    bool isAuthenticated;
    string? authError = null;

    // Check for fallback mode (useful when PAM is not available)
    var allowAny = Environment.GetEnvironmentVariable("SHELLSPECTER_ALLOW_ANY_LOGIN");
    if (string.Equals(allowAny, "true", StringComparison.OrdinalIgnoreCase))
    {
        isAuthenticated = true;
        logger.LogWarning("SHELLSPECTER_ALLOW_ANY_LOGIN is enabled — accepting login for {User}", request.Username);
    }
    else if (OperatingSystem.IsLinux())
    {
        try
        {
            isAuthenticated = PamAuth.Authenticate(request.Username, request.Password);
            if (!isAuthenticated)
                authError = "Invalid credentials";
        }
        catch (Exception ex)
        {
            isAuthenticated = false;
            authError = $"PAM error: {ex.Message}";
            logger.LogError(ex, "PAM authentication failed for user {User}", request.Username);
        }
    }
    else
    {
        // Demo mode: accept any login with non-empty credentials
        isAuthenticated = true;
    }

    if (!isAuthenticated)
    {
        return Results.Json(new { error = authError ?? "Authentication failed" }, statusCode: 401);
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
