using SanalPOS.Domain.Entities;

namespace SanalPOS.Application.Payments.Dtos;

public sealed record PaymentResultDto(
    Guid TransactionId,
    string Status,
    string? BankAuthCode,
    decimal CommissionAmount,
    decimal NetAmount,
    DateTime? CompletedAt);

public sealed record TransactionDto(
    Guid Id,
    Guid MerchantId,
    Guid TerminalId,
    string OrderReference,
    decimal Amount,
    string Currency,
    int InstallmentCount,
    string TransactionType,
    string Status,
    string MaskedCardNumber,
    string CardHolderName,
    string? BankAuthCode,
    string BankProviderCode,
    decimal CommissionAmount,
    decimal NetAmount,
    decimal RefundedTotal,
    DateTime RequestedAt,
    DateTime? CompletedAt)
{
    public static TransactionDto FromEntity(PaymentTransaction tx) => new(
        tx.Id, tx.MerchantId, tx.TerminalId, tx.OrderReference,
        tx.Amount.Amount, tx.Amount.Currency, tx.InstallmentCount,
        tx.TransactionType.ToString(), tx.Status.ToString(),
        tx.MaskedCard.Value, tx.CardHolderName, tx.BankAuthCode, tx.BankProviderCode,
        tx.CommissionAmount, tx.NetAmount, tx.RefundedTotal, tx.RequestedAt, tx.CompletedAt);
}

public sealed record StatusHistoryDto(string OldStatus, string NewStatus, DateTime ChangedAt, string ChangedBy);

public sealed record RefundResultDto(Guid RefundTransactionId, Guid OriginalTransactionId, decimal RefundAmount, string Status, string TransactionStatus);
