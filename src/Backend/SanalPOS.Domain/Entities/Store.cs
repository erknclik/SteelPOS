using SanalPOS.Domain.Common;
using SanalPOS.Domain.Exceptions;

namespace SanalPOS.Domain.Entities;

public class Store : BaseEntity, IAuditableEntity
{
    public Guid MerchantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }

    public virtual ICollection<Terminal> Terminals { get; protected set; } = new List<Terminal>();

    protected Store()
    {
    }

    public Store(Guid merchantId, string name, string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Mağaza adı boş olamaz.");

        MerchantId = merchantId;
        Name = name.Trim();
        Address = address;
    }
}
