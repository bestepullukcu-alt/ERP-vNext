namespace Diten.PpmService.Domain.Repositories;

public sealed record AuditIntent(
    Guid Id,
    Guid TenantId,
    Guid ActorId,
    Guid CorrelationId,
    string EntityType,
    Guid EntityId,
    string Mutation,
    DateTime OccurredAtUtc);
