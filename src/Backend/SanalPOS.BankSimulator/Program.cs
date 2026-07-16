using Microsoft.Extensions.Logging;
using SanalPOS.BankSimulator;
using SanalPOS.Infrastructure.Iso8583.Dialects;

// Sahte banka host'u: yerel geliştirme ve sertifikasyon senaryoları için.
// Kullanım: dotnet run [-- port] ; ISO8583_SIM_PORT ve ISO8583_SIM_DIALECT
// ortam değişkenleri de desteklenir (Docker Compose bunları kullanır).
var port = args.Length > 0 && int.TryParse(args[0], out var argPort)
    ? argPort
    : int.TryParse(Environment.GetEnvironmentVariable("ISO8583_SIM_PORT"), out var envPort) ? envPort : 8583;

var dialectName = Environment.GetEnvironmentVariable("ISO8583_SIM_DIALECT") ?? Iso8583Dialects.Iso87AsciiName;
var dialect = new Iso8583DialectRegistry().Get(dialectName);

using var loggerFactory = LoggerFactory.Create(builder => builder
    .SetMinimumLevel(LogLevel.Information)
    .AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss "));

await using var engine = new BankSimulatorEngine(dialect, loggerFactory.CreateLogger("BankSimulator"), port);
engine.Start();

var shutdown = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.TrySetResult();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.TrySetResult();

await shutdown.Task;
