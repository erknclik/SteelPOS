using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Infrastructure.Iso8583.Adapters;
using SanalPOS.Infrastructure.Iso8583.Dialects;
using SanalPOS.Infrastructure.Iso8583.Network;

namespace SanalPOS.Infrastructure.Iso8583;

/// <summary>
/// "Iso8583:Banks" konfigürasyonundaki her etkin kayıt için bir Iso8583BankAdapter
/// oluşturup IBankProviderAdapter olarak kaydeder; BankAdapterFactory bunları
/// ProviderCode üzerinden çözer. Hatalı konfigürasyon açılışta patlar (fail-fast).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddIso8583BankAdapters(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IIso8583DialectRegistry, Iso8583DialectRegistry>();

        var bankConfigs = configuration.GetSection(Iso8583BankOptions.SectionName).GetChildren().ToList();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var bankConfig in bankConfigs)
        {
            var options = bankConfig.Get<Iso8583BankOptions>()
                          ?? throw new InvalidOperationException($"{Iso8583BankOptions.SectionName}: kayıt bağlanamadı (index {bankConfig.Key}).");

            if (!options.Enabled)
                continue;

            options.Validate();

            if (!seenCodes.Add(options.ProviderCode))
                throw new InvalidOperationException(
                    $"{Iso8583BankOptions.SectionName}: '{options.ProviderCode}' birden fazla kez tanımlanmış.");

            services.AddSingleton<IBankProviderAdapter>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var spec = sp.GetRequiredService<IIso8583DialectRegistry>().Get(options.Dialect);

                var channel = new TcpIso8583Channel(options.Channel, spec, loggerFactory.CreateLogger<TcpIso8583Channel>());

                return new Iso8583BankAdapter(
                    options,
                    channel,
                    new InMemoryStanSequence(),
                    TimeProvider.System,
                    loggerFactory.CreateLogger<Iso8583BankAdapter>());
            });
        }

        return services;
    }
}
