using SanalPOS.Domain.Common;
using SanalPOS.Domain.Exceptions;

namespace SanalPOS.Domain.Entities;

public class CommissionRule : BaseEntity, IAuditableEntity
{
    public Guid MerchantId { get; private set; }
    public short InstallmentCount { get; private set; }
    public decimal Rate { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }

    protected CommissionRule()
    {
    }

    public CommissionRule(Guid merchantId, short installmentCount, decimal rate, DateOnly validFrom, DateOnly? validTo)
    {
        if (installmentCount is < 1 or > 12)
            throw new DomainException("Taksit sayısı 1-12 aralığında olmalıdır.");
        if (rate is < 0 or > 100)
            throw new DomainException("Komisyon oranı 0-100 aralığında olmalıdır.");
        if (validTo is not null && validTo < validFrom)
            throw new DomainException("Geçerlilik bitişi başlangıçtan önce olamaz.");

        MerchantId = merchantId;
        InstallmentCount = installmentCount;
        Rate = rate;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }
}
