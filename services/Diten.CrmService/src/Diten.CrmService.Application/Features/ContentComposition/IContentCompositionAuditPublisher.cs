namespace Diten.CrmService.Application.Features.ContentComposition;

/// <summary>
/// SCMM-12 (CAND-CAP-0011) — seam for emitting MOD-0021 audit events for ContentComposition (Marketing owner) actions.
/// Mirrors <c>IKnowledgeConceptAuditPublisher</c>: CRM owns NO audit store; the Infrastructure implementation forwards to
/// the MOD-0021 audit append contract with <c>SourceModule = "CAND-CAP-0011"</c> (the capability that owns claims and
/// composition, NOT the MOD-0162 knowledge module). The event carries actor (via the forwarded principal/header) +
/// tenant + UTC + object type/id/version + correlation. Emission is fail-soft (an audit outage never breaks the write).
/// </summary>
public interface IContentCompositionAuditPublisher
{
    Task PublishAsync(
        string eventName,
        Guid tenantId,
        string entityType,
        Guid entityId,
        int version,
        string? detail,
        CancellationToken cancellationToken);
}

/// <summary>Object-type tags carried on ContentComposition audit events (the <c>EntityType</c> of the append contract).</summary>
public static class ContentCompositionAuditEntities
{
    public const string Claim = "Claim";
}
