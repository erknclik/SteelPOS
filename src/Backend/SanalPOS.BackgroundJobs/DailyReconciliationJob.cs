using MassTransit;
using SanalPOS.Contracts;

namespace SanalPOS.BackgroundJobs;

/// <summary>
/// Her gün UTC 03:00'te günlük mutabakat sürecini tetikleyen event yayınlar.
/// Reconciliation modülü bu event'i tüketerek banka mutabakat karşılaştırmasını çalıştırır (ileri faz).
/// </summary>
public class DailyReconciliationJob : BackgroundService
{
    private static readonly TimeSpan TriggerTimeUtc = TimeSpan.FromHours(3);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyReconciliationJob> _logger;

    public DailyReconciliationJob(IServiceProvider serviceProvider, ILogger<DailyReconciliationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.Add(TriggerTimeUtc);
            if (nextRun <= now)
                nextRun = nextRun.AddDays(1);

            _logger.LogInformation("Sonraki mutabakat tetikleme zamanı: {NextRun:O}", nextRun);

            try
            {
                await Task.Delay(nextRun - now, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
                await publishEndpoint.Publish(new DailyReconciliationRequestedEvent(
                    DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                    DateTime.UtcNow,
                    Guid.NewGuid().ToString()), stoppingToken);

                _logger.LogInformation("DailyReconciliationRequestedEvent yayınlandı.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mutabakat event'i yayınlanamadı; bir sonraki döngüde tekrar denenecek.");
            }
        }
    }
}
