using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using NLog.Web;

namespace SanalPOS.Infrastructure.Logging.NLog;

public static class NLogConfigurator
{
    /// <summary>
    /// NLog'u ILogger&lt;T&gt; soyutlamasının arkasına provider olarak takar.
    /// Konfigürasyon nlog.config dosyasından okunur.
    /// </summary>
    public static void UseSanalPosNLog(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Host.UseNLog();
    }
}
