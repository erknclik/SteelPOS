using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SanalPOS.Domain.Interfaces;
using SanalPOS.Infrastructure.EfCore.Interceptors;
using SanalPOS.Infrastructure.EfCore.Repositories;

namespace SanalPOS.Infrastructure.EfCore;

public static class DependencyInjection
{
    public static IServiceCollection AddEfCorePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<SanalPosDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                    configuration.GetConnectionString("Default"),
                    npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", SanalPosDbContext.Schema))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());

            if (configuration.GetValue<bool>("Persistence:EnableSensitiveDataLogging"))
                options.EnableSensitiveDataLogging();
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPaymentTransactionRepository, EfPaymentTransactionRepository>();
        services.AddScoped<IMerchantRepository, EfMerchantRepository>();
        services.AddScoped<IStoreRepository, EfStoreRepository>();
        services.AddScoped<ITerminalRepository, EfTerminalRepository>();
        services.AddScoped<IRefundTransactionRepository, EfRefundTransactionRepository>();
        services.AddScoped<ICommissionRuleRepository, EfCommissionRuleRepository>();
        services.AddScoped<IWebhookSubscriptionRepository, EfWebhookSubscriptionRepository>();
        services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IRoleRepository, EfRoleRepository>();
        services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();

        return services;
    }
}
