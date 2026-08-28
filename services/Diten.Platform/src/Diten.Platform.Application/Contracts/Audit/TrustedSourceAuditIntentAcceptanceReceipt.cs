namespace Diten.Platform.Application.Contracts.Audit;

public sealed record TrustedSourceAuditIntentAcceptanceReceipt(
    string CentralAcknowledgement,
    string CentralIdempotencyKey,
    string ContractVersion,
    DateTimeOffset AcceptedAt,
    bool Duplicate);
