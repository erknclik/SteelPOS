using SanalPOS.Domain.Entities;

namespace SanalPOS.Application.Merchants.Dtos;

public sealed record MerchantDto(
    Guid Id,
    string Name,
    string TaxNumber,
    string Iban,
    string Status,
    decimal DefaultCommissionRate,
    DateTime CreatedAt)
{
    public static MerchantDto FromEntity(Merchant m) => new(
        m.Id, m.Name, m.TaxNumber, m.Iban.Value, m.Status.ToString(), m.DefaultCommissionRate, m.CreatedAt);
}

public sealed record StoreDto(Guid Id, Guid MerchantId, string Name, string? Address)
{
    public static StoreDto FromEntity(Store s) => new(s.Id, s.MerchantId, s.Name, s.Address);
}

public sealed record TerminalDto(Guid Id, Guid StoreId, string TerminalCode, string BankProviderCode, bool IsActive)
{
    public static TerminalDto FromEntity(Terminal t) => new(t.Id, t.StoreId, t.TerminalCode, t.BankProviderCode, t.IsActive);
}

public sealed record CommissionRuleDto(Guid Id, Guid MerchantId, short InstallmentCount, decimal Rate, DateOnly ValidFrom, DateOnly? ValidTo)
{
    public static CommissionRuleDto FromEntity(CommissionRule r) => new(r.Id, r.MerchantId, r.InstallmentCount, r.Rate, r.ValidFrom, r.ValidTo);
}
