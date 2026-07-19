using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Reconciliation;

/// <summary>
/// Gün sonu mutabakatını çalıştırır: verilen günün işlem toplamları banka sağlayıcısı +
/// para birimi bazında hesaplanır ve her bankaya batch-close (ISO 8583'te 0500) gönderilir.
/// Banka toplamları kendi defteriyle karşılaştırır; uyumsuzlukta sonuç IsBalanced=false
/// döner ve fark operasyon ekibince incelenir. Gün verilmezse dün (UTC) mutabakatlanır.
/// </summary>
public sealed record RunReconciliationCommand(DateOnly? Day = null, string? ProviderCode = null)
    : IRequest<IReadOnlyList<ReconciliationResultDto>>;

public sealed record ReconciliationResultDto(
    string ProviderCode,
    string Currency,
    DateOnly Day,
    int SaleCount,
    decimal SaleAmount,
    int RefundCount,
    decimal RefundAmount,
    int VoidCount,
    decimal VoidAmount,
    bool IsBalanced,
    string? ReasonCode,
    string? ReasonMessage);

public sealed class RunReconciliationCommandValidator : AbstractValidator<RunReconciliationCommand>
{
    public RunReconciliationCommandValidator()
    {
        RuleFor(x => x.Day)
            .Must(d => d is null || d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Gelecek bir gün mutabakatlanamaz.");
        RuleFor(x => x.ProviderCode).MaximumLength(50);
    }
}

public class RunReconciliationCommandHandler : IRequestHandler<RunReconciliationCommand, IReadOnlyList<ReconciliationResultDto>>
{
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IRefundTransactionRepository _refundRepository;
    private readonly IReconciliationRunRepository _runRepository;
    private readonly IBankAdapterFactory _bankAdapterFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RunReconciliationCommandHandler> _logger;

    public RunReconciliationCommandHandler(
        IPaymentTransactionRepository transactionRepository,
        IRefundTransactionRepository refundRepository,
        IReconciliationRunRepository runRepository,
        IBankAdapterFactory bankAdapterFactory,
        IUnitOfWork unitOfWork,
        ILogger<RunReconciliationCommandHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _refundRepository = refundRepository;
        _runRepository = runRepository;
        _bankAdapterFactory = bankAdapterFactory;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ReconciliationResultDto>> Handle(RunReconciliationCommand request, CancellationToken ct)
    {
        var day = request.Day ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var fromUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = fromUtc.AddDays(1);

        var paymentTotals = await _transactionRepository.GetProviderDailyTotalsAsync(fromUtc, toUtc, ct);
        var refundTotals = await _refundRepository.GetProviderDailyTotalsAsync(fromUtc, toUtc, ct);

        // Satış/iptal ve iade toplamlarını (sağlayıcı, para birimi) anahtarıyla birleştir.
        var keys = paymentTotals.Select(p => (p.BankProviderCode, p.Currency))
            .Union(refundTotals.Select(r => (r.BankProviderCode, r.Currency)))
            .Where(k => request.ProviderCode is null
                        || string.Equals(k.BankProviderCode, request.ProviderCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k.BankProviderCode).ThenBy(k => k.Currency)
            .ToList();

        var results = new List<ReconciliationResultDto>(keys.Count);
        foreach (var (providerCode, currency) in keys)
        {
            var payment = paymentTotals.FirstOrDefault(p => p.BankProviderCode == providerCode && p.Currency == currency);
            var refund = refundTotals.FirstOrDefault(r => r.BankProviderCode == providerCode && r.Currency == currency);

            var totals = new SettlementTotals(
                day, currency,
                payment?.SaleCount ?? 0, payment?.SaleAmount ?? 0m,
                refund?.RefundCount ?? 0, refund?.RefundAmount ?? 0m,
                payment?.VoidCount ?? 0, payment?.VoidAmount ?? 0m);

            var result = await SettleAsync(providerCode, totals, ct);
            results.Add(result);

            // Koşum sonucu kalıcıdır: dengesiz kayıtlar operasyonun inceleme kuyruğudur.
            await _runRepository.AddAsync(new ReconciliationRun(
                day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                result.ProviderCode, result.Currency,
                result.SaleCount, result.SaleAmount,
                result.RefundCount, result.RefundAmount,
                result.VoidCount, result.VoidAmount,
                result.IsBalanced, result.ReasonCode, result.ReasonMessage), ct);
        }

        if (results.Count > 0)
            await _unitOfWork.SaveChangesAsync(ct);

        return results;
    }

    private async Task<ReconciliationResultDto> SettleAsync(string providerCode, SettlementTotals totals, CancellationToken ct)
    {
        BankOperationResult result;
        try
        {
            var adapter = _bankAdapterFactory.Resolve(providerCode);
            result = await adapter.SettleAsync(totals, ct);
        }
        catch (Exception ex)
        {
            // Tek bankanın hatası diğer bankaların mutabakatını engellememeli.
            _logger.LogError(ex, "Mutabakat gönderilemedi. Provider: {Provider}, Gün: {Day}", providerCode, totals.Day);
            result = new BankOperationResult(false, "ERROR", ex.Message);
        }

        if (result.IsSuccessful)
            _logger.LogInformation("Mutabakat dengede. Provider: {Provider}, Gün: {Day}", providerCode, totals.Day);
        else
            _logger.LogWarning("Mutabakat DENGEDE DEĞİL. Provider: {Provider}, Gün: {Day}, Kod: {Code}, Mesaj: {Message}",
                providerCode, totals.Day, result.ReasonCode, result.ReasonMessage);

        return new ReconciliationResultDto(
            providerCode, totals.Currency, totals.Day,
            totals.SaleCount, totals.SaleAmount,
            totals.RefundCount, totals.RefundAmount,
            totals.VoidCount, totals.VoidAmount,
            result.IsSuccessful, result.ReasonCode, result.ReasonMessage);
    }
}
