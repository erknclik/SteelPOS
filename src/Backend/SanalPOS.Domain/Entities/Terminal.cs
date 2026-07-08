using SanalPOS.Domain.Common;
using SanalPOS.Domain.Exceptions;

namespace SanalPOS.Domain.Entities;

public class Terminal : BaseEntity, IAuditableEntity
{
    public Guid StoreId { get; private set; }
    public string TerminalCode { get; private set; } = string.Empty;
    public string BankProviderCode { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    protected Terminal()
    {
    }

    public Terminal(Guid storeId, string terminalCode, string bankProviderCode)
    {
        if (string.IsNullOrWhiteSpace(terminalCode))
            throw new DomainException("Terminal kodu boş olamaz.");
        if (string.IsNullOrWhiteSpace(bankProviderCode))
            throw new DomainException("Banka sağlayıcı kodu boş olamaz.");

        StoreId = storeId;
        TerminalCode = terminalCode.Trim();
        BankProviderCode = bankProviderCode.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
