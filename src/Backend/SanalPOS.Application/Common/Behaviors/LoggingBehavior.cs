using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace SanalPOS.Application.Common.Behaviors;

/// <summary>
/// Her request'in başlangıç/bitişini ve süresini loglar. Request içeriği loglanmaz —
/// kart verisi gibi hassas alanların log'a sızmaması için sadece tip adı yazılır.
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("İşleniyor: {RequestName}", requestName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            _logger.LogInformation("Tamamlandı: {RequestName} ({ElapsedMs} ms)", requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hata: {RequestName} ({ElapsedMs} ms)", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
