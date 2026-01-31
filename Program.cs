using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using wenu.Services;
using Scalar.AspNetCore;
using wenu.Configs;
using wenu.Middleware;
using wenu.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ─── SignalR ──────────────────────────────────────────────────────────────

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 1024 * 1024 * 10;
    options.StreamBufferCapacity = 100;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    options.MaximumParallelInvocationsPerClient = 10;
})
.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.WriteIndented = false;
});

// ─── CORS ─────────────────────────────────────────────────────────────────

builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRCors", policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("X-SignalR-Connection-Id");
    });
});

// ─── Controllers & Validation ─────────────────────────────────────────────

builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>();

// ─── In-Memory User Store (replaces EF + Identity entirely) ──────────────
//     Singleton so the same dictionary lives for the lifetime of the process.

builder.Services.AddSingleton<InMemoryUserStore>();
builder.Services.AddSingleton<InMemoryDataStore>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<wenu.Entities.Users>, Microsoft.AspNetCore.Identity.PasswordHasher<wenu.Entities.Users>>();

// ─── JWT Authentication ───────────────────────────────────────────────────

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
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

// ─── Application services ────────────────────────────────────────────────

builder.Services.Configure<FirewallSettings>(
    builder.Configuration.GetSection("FirewallSettings"));

builder.Services.AddSingleton<AttackPatternDetector>();
builder.Services.AddSingleton<MediaServer>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ─── Caching & Compression ────────────────────────────────────────────────

builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024 * 1024 * 512;
    options.CompactionPercentage = 0.25;
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// ─── OpenAPI ──────────────────────────────────────────────────────────────

builder.Services.AddOpenApi();

// ═══════════════════════════════════════════════════════════════════════════
// PIPELINE
// ═══════════════════════════════════════════════════════════════════════════

var app = builder.Build();

app.UseResponseCompression();
app.UseCors("SignalRCors");

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<StreamingHub>("/hubs/streaming");

app.Run();