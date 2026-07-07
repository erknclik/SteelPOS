using MediatR;
using SanalPOS.Application.Payments.Dtos;
using SanalPOS.Domain.Enums;

namespace SanalPOS.Application.Payments.Commands.CreatePayment;

/// <summary>
/// Satış veya ön provizyon işlemi oluşturur. CardNumber/Cvv sadece banka adaptörüne iletilir;
/// loglanmaz, cache'lenmez, veritabanına maskeli hali yazılır.
/// </summary>
public sealed record CreatePaymentCommand(
    Guid MerchantId,
    Guid TerminalId,
    string OrderReference,
    decimal Amount,
    string Currency,
    short InstallmentCount,
    string CardNumber,
    string CardHolderName,
    int ExpireMonth,
    int ExpireYear,
    string Cvv,
    string IdempotencyKey,
    TransactionType TransactionType = TransactionType.Sale) : IRequest<PaymentResultDto>;
