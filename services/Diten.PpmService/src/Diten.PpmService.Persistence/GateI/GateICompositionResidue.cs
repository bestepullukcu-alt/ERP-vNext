namespace Diten.PpmService.Persistence.GateI;


public sealed record GateICompositionResidue(
    int RelationshipCount,
    int ReceiptCount,
    int AuditIntentCount,
    int OutboxCount);
