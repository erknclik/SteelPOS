using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Formatting.Compact;

namespace SanalPOS.Infrastructure.Logging.Serilog;

public static class SerilogConfigurator
{
    /// <summary>
    /// Serilog'u ILogger&lt;T&gt; soyutlamasının arkasına provider olarak takar.
    /// appsettings'teki "Serilog" bölümünden okur; Console + File + Seq sink'lerini kurar.
    /// </summary>
    public static void UseSanalPosSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Application", "SanalPOS")
                .WriteTo.Console(new CompactJsonFormatter())
                .WriteTo.File(
                    path: "logs/sanalpos-.log",
                    rollingInterval: RollingInterval.Day,
                    formatter: new CompactJsonFormatter());

            var seqUrl = context.Configuration["Logging:Serilog:SeqUrl"];
            if (!string.IsNullOrWhiteSpace(seqUrl))
                configuration.WriteTo.Seq(seqUrl);
        });
    }
}
