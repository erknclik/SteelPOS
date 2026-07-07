using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using SanalPOS.Domain.Interfaces;
using SanalPOS.Infrastructure.NHibernate.Repositories;

namespace SanalPOS.Infrastructure.NHibernate;

public static class DependencyInjection
{
    public static IServiceCollection AddNHibernatePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil.");

        services.AddSingleton(_ => NHibernateSessionFactoryProvider.Build(connectionString));
        services.AddScoped(sp => sp.GetRequiredService<ISessionFactory>().OpenSession());

        services.AddScoped<IUnitOfWork, NhUnitOfWork>();
        services.AddScoped<IPaymentTransactionRepository, NhPaymentTransactionRepository>();
        services.AddScoped<IMerchantRepository, NhMerchantRepository>();
        services.AddScoped<IStoreRepository, NhStoreRepository>();
        services.AddScoped<ITerminalRepository, NhTerminalRepository>();
        services.AddScoped<IRefundTransactionRepository, NhRefundTransactionRepository>();
        services.AddScoped<ICommissionRuleRepository, NhCommissionRuleRepository>();
        services.AddScoped<IWebhookSubscriptionRepository, NhWebhookSubscriptionRepository>();
        services.AddScoped<IAuditLogRepository, NhAuditLogRepository>();
        services.AddScoped<IUserRepository, NhUserRepository>();
        services.AddScoped<IRoleRepository, NhRoleRepository>();
        services.AddScoped<IRefreshTokenRepository, NhRefreshTokenRepository>();

        return services;
    }
}
