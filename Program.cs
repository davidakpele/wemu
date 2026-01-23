using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using wenu.Entities;
using wenu.Middleware;
using wenu.Services;
using wenu.Validators;
using Scalar.AspNetCore;
using wumo.Configs;
using wumo.Services;
using wenu.Enums;

var builder = WebApplication.CreateBuilder(args);

// Controllers + FluentValidation
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>();

// DbContext (PostgreSQL)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<Users, IdentityRole<int>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] 
    ?? throw new InvalidOperationException("JWT SecretKey missing");

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
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero // No tolerance for expired tokens
    };

    // Custom JWT error handling
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();

            object errorResponse;
            var exceptionMessage = context.Exception.Message;

            // Log the actual error for debugging (server-side only)
            logger.LogWarning(
                "JWT Authentication failed: {Exception} | Path: {Path}", 
                context.Exception.Message, 
                context.Request.Path
            );

            if (context.Exception is SecurityTokenExpiredException)
            {
                errorResponse = new
                {
                    error = "TokenExpired",
                    message = "Your session has expired. Please login again.",
                    timestamp = DateTime.UtcNow
                };
                context.Response.Headers.Append("Token-Expired", "true");
            }
            else if (context.Exception is SecurityTokenInvalidSignatureException)
            {
                errorResponse = new
                {
                    error = "InvalidToken",
                    message = "The provided token is invalid. Please login again.",
                    timestamp = DateTime.UtcNow
                };
            }
            else if (context.Exception is SecurityTokenNotYetValidException)
            {
                errorResponse = new
                {
                    error = "TokenNotYetValid",
                    message = "The token is not valid yet. Please check your system time.",
                    timestamp = DateTime.UtcNow
                };
            }
            else if (context.Exception is SecurityTokenInvalidAudienceException)
            {
                errorResponse = new
                {
                    error = "InvalidToken",
                    message = "The token is not valid for this application.",
                    timestamp = DateTime.UtcNow
                };
            }
            else if (context.Exception is SecurityTokenInvalidIssuerException)
            {
                errorResponse = new
                {
                    error = "InvalidToken",
                    message = "The token was not issued by a trusted source.",
                    timestamp = DateTime.UtcNow
                };
            }
            else if (exceptionMessage.Contains("Unable to decode") || 
                     exceptionMessage.Contains("IDX") ||
                     exceptionMessage.Contains("Base64"))
            {
                // Malformed token
                errorResponse = new
                {
                    error = "MalformedToken",
                    message = "The token format is invalid. Please login again.",
                    timestamp = DateTime.UtcNow
                };
            }
            else if (exceptionMessage.Contains("Lifetime validation failed") ||
                     exceptionMessage.Contains("NotBefore") ||
                     exceptionMessage.Contains("Expires"))
            {
                errorResponse = new
                {
                    error = "TokenExpired",
                    message = "Your session has expired. Please login again.",
                    timestamp = DateTime.UtcNow
                };
            }
            else
            {
                // Generic token validation error - don't expose internal details
                errorResponse = new
                {
                    error = "InvalidToken",
                    message = "The provided token is invalid. Please login again.",
                    timestamp = DateTime.UtcNow
                };
            }

            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            
            return context.Response.WriteAsync(
                JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                })
            );
        },
        
        OnChallenge = context =>
        {
            // Skip the default behavior
            context.HandleResponse();

            // Only write response if it hasn't started
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";

                var errorResponse = new
                {
                    error = "Unauthorized",
                    message = "Authentication is required to access this resource. Please login.",
                    timestamp = DateTime.UtcNow
                };

                return context.Response.WriteAsync(
                    JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                    })
                );
            }

            return Task.CompletedTask;
        },

        OnForbidden = context =>
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";

            var errorResponse = new
            {
                error = "Forbidden",
                message = "You don't have permission to access this resource.",
                timestamp = DateTime.UtcNow
            };

            return context.Response.WriteAsync(
                JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                })
            );
        },

        OnMessageReceived = context =>
        {
            // Optional: Handle tokens from query string (for WebSocket/SignalR)
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            
            if (!string.IsNullOrEmpty(accessToken) && 
                path.StartsWithSegments("/hubs")) // Adjust path as needed
            {
                context.Token = accessToken;
            }
            
            return Task.CompletedTask;
        }
    };
});

// Application services
builder.Services.Configure<FirewallSettings>(
    builder.Configuration.GetSection("FirewallSettings"));
builder.Services.AddSingleton<AttackPatternDetector>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// OpenAPI / Scalar
builder.Services.AddOpenApi();

var app = builder.Build();

// **Seed Roles on Application Startup**
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var roleSeeder = new RoleSeeder(roleManager);
        await roleSeeder.SeedRolesAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding roles.");
    }
}

// Middleware
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthentication(); // Must come before UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.Run();