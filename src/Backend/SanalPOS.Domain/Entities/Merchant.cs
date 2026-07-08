using SanalPOS.Domain.Common;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Events;
using SanalPOS.Domain.Exceptions;
using SanalPOS.Domain.ValueObjects;

namespace SanalPOS.Domain.Entities;

public class Merchant : BaseEntity, IAuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string TaxNumber { get; private set; } = string.Empty;
    public Iban Iban { get; private set; } = null!;
    public MerchantStatus Status { get; private set; } = MerchantStatus.Active;
    public decimal DefaultCommissionRate { get; private set; }

    public virtual ICollection<Store> Stores { get; protected set; } = new List<Store>();
    public virtual ICollection<CommissionRule> CommissionRules { get; protected set; } = new List<CommissionRule>();

    protected Merchant()
    {
    }

    public Merchant(string name, string taxNumber, Iban iban, decimal defaultCommissionRate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("İşyeri adı boş olamaz.");
        if (defaultCommissionRate is < 0 or > 100)
            throw new DomainException("Komisyon oranı 0-100 aralığında olmalıdır.");

        Name = name.Trim();
        TaxNumber = taxNumber.Trim();
        Iban = iban;
        DefaultCommissionRate = defaultCommissionRate;
    }

    public void Update(string name, string taxNumber, Iban iban, decimal defaultCommissionRate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("İşyeri adı boş olamaz.");
        if (defaultCommissionRate is < 0 or > 100)
            throw new DomainException("Komisyon oranı 0-100 aralığında olmalıdır.");

        Name = name.Trim();
        TaxNumber = taxNumber.Trim();
        Iban = iban;
        DefaultCommissionRate = defaultCommissionRate;
    }

    public void Suspend()
    {
        if (Status == MerchantStatus.Closed)
            throw new DomainException("Kapalı işyeri askıya alınamaz.");
        if (Status == MerchantStatus.Suspended)
            return;

        Status = MerchantStatus.Suspended;
        AddDomainEvent(new MerchantSuspendedDomainEvent(this));
    }

    public void Activate()
    {
        if (Status == MerchantStatus.Closed)
            throw new DomainException("Kapalı işyeri tekrar aktive edilemez.");
        Status = MerchantStatus.Active;
    }

    /// <summary>Taksit sayısına göre geçerli komisyon oranını döndürür; kural yoksa varsayılan oran kullanılır.</summary>
    public decimal ResolveCommissionRate(int installmentCount, DateTime onDateUtc)
    {
        var rule = CommissionRules
            .Where(r => !r.IsDeleted
                        && r.InstallmentCount == installmentCount
                        && r.ValidFrom <= DateOnly.FromDateTime(onDateUtc)
                        && (r.ValidTo is null || r.ValidTo >= DateOnly.FromDateTime(onDateUtc)))
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefault();

        return rule?.Rate ?? DefaultCommissionRate;
    }
}
