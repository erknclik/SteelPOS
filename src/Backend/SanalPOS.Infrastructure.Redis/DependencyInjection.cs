using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SanalPOS.Application.Common.Interfaces;
using StackExchange.Redis;

namespace SanalPOS.Infrastructure.Redis;

public static class DependencyInjection
{
    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis tanımlı değil.");

        // abortConnect=false: Redis geçici erişilemezse uygulama açılışı engellenmez.
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(connectionString));

        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<IDistributedLockService, RedisDistributedLockService>();

        // ASP.NET Core IDistributedCache (session vb. için)
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = "SanalPOS:";
        });

        return services;
    }
}
