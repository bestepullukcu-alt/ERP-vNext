namespace Diten.MdmService.Domain.Repositories;

public sealed record AuditIntentAcknowledgement(
    string CentralAcknowledgement,
    string CentralIdempotencyKey,
    string ContractVersion,
    DateTimeOffset AcceptedAt);
