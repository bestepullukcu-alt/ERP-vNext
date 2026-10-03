namespace Diten.SupplyChainService.Domain.Features.Claims;
public sealed record ClaimAuditEntry(Guid Id, Guid TenantId, Guid LegalEntityId, Guid ClaimId, Guid ActorId, string? BeforeStatus, string AfterStatus, string? ApprovedAmount, string? ResolutionCode, string? Note, string OccurredAt, DateTimeOffset ReceivedAt, DateTimeOffset CommittedAt, Guid CorrelationRoot, Guid EventId);
