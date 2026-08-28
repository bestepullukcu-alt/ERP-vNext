using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU07 — monotonic sequence counter for a (tenant, identifier type, prefix, domain, type) key. Incremented
/// ATOMICALLY (Mongo <c>FindOneAndUpdate $inc</c>) so concurrent allocations never collide, and it NEVER rolls back —
/// a cancelled/abandoned/deleted allocation leaves the counter advanced, so the number is never reused (SOP §6.3;
/// gaps are permitted, reuse is not). One counter per distinct key per tenant.
/// </summary>
public sealed class DocumentIdentifierSequenceCounter : TenantScopedEntity
{
    public DocumentIdentifierType IdentifierType { get; set; }
    public string? Prefix { get; set; }
    public string? DomainCode { get; set; }
    public string? TypeCode { get; set; }

    /// <summary>Last handed-out number. The next allocation is <c>NextNumber + 1</c> written back atomically.</summary>
    public long NextNumber { get; set; }
}
