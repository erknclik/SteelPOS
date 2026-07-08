using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SanalPOS.API.Middleware;
using SanalPOS.API.Setup;
using SanalPOS.Application;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Infrastructure;
using SanalPOS.Infrastructure.Logging.NLog;
using SanalPOS.Infrastructure.Logging.Serilog;
using SanalPOS.Infrastructure.Redis;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1) Loglama: provider seçimine göre Serilog veya NLog (bkz. docs/05-loglama.md)
var loggingProvider = builder.Configuration["Logging:Provider"] ?? "Serilog";
switch (loggingProvider)
{
    case "NLog":
        builder.UseSanalPosNLog();
        break;
    case "Serilog":
        builder.UseSanalPosSerilog();
        break;
    default:
        throw new InvalidOperationException(
            $"Desteklenmeyen Logging:Provider değeri: '{loggingProvider}'. Geçerli değerler: 'Serilog', 'NLog'.");
}

// 2) Application katmanı (MediatR, FluentValidation, pipeline behavior'lar)
builder.Services.AddApplicationServices();

// 3) Ortak infrastructure + Persistence (EfCore/NHibernate) + Cache + Messaging (RabbitMq/Kafka)
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddRedisCache(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);

// 4) Kimlik doğrulama / yetkilendirme (JWT Bearer + Redis jti blacklist kontrolü)
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey tanımlı değil (user-secrets veya ortam değişkeni kullanın).");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                // Logout edilen token'lar Redis blacklist'ten kontrol edilir.
                var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrEmpty(jti))
                    return;

                var cache = context.HttpContext.RequestServices.GetRequiredService<ICacheService>();
                if (await cache.ExistsAsync(CacheKeys.JwtBlacklist(jti)))
                    context.Fail("Token iptal edilmiş.");
            }
        };
    });
builder.Services.AddAuthorization();

// 5) API altyapısı: versiyonlama, rate limiting, CORS, swagger, health checks
builder.Services.AddControllers();

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
var windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            // Merchant bazlı; anonim isteklerde IP bazlı partition.
            httpContext.User.FindFirst("merchant_id")?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                SegmentsPerWindow = 4,
                QueueLimit = 0
            }));
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                 ?? ["http://localhost:5173", "http://localhost:3000"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("X-Total-Count", "X-Correlation-Id")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SanalPOS API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT access token girin."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
    options.CustomSchemaIds(t => t.FullName?.Replace("+", "."));
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")!, name: "postgresql", tags: ["ready"])
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!, name: "redis", tags: ["ready"]);

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

if (loggingProvider == "Serilog")
    app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "SanalPOS API v1"));

app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

// Development: şema migrate edilir ve demo veri seed edilir (sadece EF Core provider'da).
if (app.Environment.IsDevelopment())
    await DevelopmentDataSeeder.SeedAsync(app.Services, app.Logger);

app.Run();

// WebApplicationFactory tabanlı integration testler için
public partial class Program
{
}
