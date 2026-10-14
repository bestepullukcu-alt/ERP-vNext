using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Repositories;

/// <summary>Appends one <see cref="InternalServiceAccessAuditRecord"/> (insert only — the audit is never edited).</summary>
public interface IInternalAccessAuditWriter
{
    Task WriteAsync(InternalServiceAccessAuditRecord record, CancellationToken cancellationToken = default);
}
