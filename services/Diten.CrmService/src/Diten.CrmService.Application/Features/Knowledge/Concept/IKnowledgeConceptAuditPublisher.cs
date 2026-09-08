namespace Diten.CrmService.Application.Features.Knowledge.Concept;

/// <summary>
/// SCMM-09 (audit bundle) — seam for emitting MOD-0021 audit events for concept-graph actions (①②). Mirrors
/// <c>IAccountAuditPublisher</c>: CRM owns NO audit store; the Infrastructure implementation forwards to the MOD-0021
/// audit append contract with <c>SourceModule = "MOD-0162"</c> (NOT the Account/Contact "MOD-0150"). The event carries
/// actor (via the forwarded principal/header) + tenant + UTC + object type/id/version + correlation. Emission is
/// fail-soft (an audit outage never breaks the business write); the durable-release requirement is a separate
/// owning-team concern (R1) and is deliberately NOT introduced here.
/// </summary>
public interface IKnowledgeConceptAuditPublisher
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

/// <summary>Object-type tags carried on concept-graph audit events (the <c>EntityType</c> of the append contract).</summary>
public static class KnowledgeConceptAuditEntities
{
    public const string ConceptType = "ConceptType";
    public const string ConceptNode = "ConceptNode";
    public const string ConceptRelationship = "ConceptRelationship";
    public const string ConceptChainTemplate = "ConceptChainTemplate";
}

/// <summary>Event names for concept-graph audit. The per-aggregate lifecycle events reuse the canonical FU03
/// <see cref="Diten.CrmService.Domain.Entities.ConceptGraphReasonCodes"/>; the combined-write adds one event.</summary>
public static class KnowledgeConceptAuditEvents
{
    public const string NodeWithRelationshipCreated = "concept_node_with_relationship_created";
}
