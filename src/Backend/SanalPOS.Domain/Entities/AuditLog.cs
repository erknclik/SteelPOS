using SanalPOS.Domain.Common;
using SanalPOS.Domain.Enums;

namespace SanalPOS.Domain.Entities;

/// <summary>Append-only denetim kaydı; asla güncellenmez/silinmez.</summary>
public class AuditLog : BaseEntity
{
    public string EntityName { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public AuditAction Action { get; private set; }
    public string PerformedBy { get; private set; } = string.Empty;
    public DateTime PerformedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>Değişiklik anındaki veri (JSON, PostgreSQL'de jsonb).</summary>
    public string PayloadSnapshot { get; private set; } = "{}";

    protected AuditLog()
    {
    }

    public AuditLog(string entityName, Guid entityId, AuditAction action, string performedBy, string payloadSnapshot)
    {
        EntityName = entityName;
        EntityId = entityId;
        Action = action;
        PerformedBy = performedBy;
        PayloadSnapshot = payloadSnapshot;
    }
}
