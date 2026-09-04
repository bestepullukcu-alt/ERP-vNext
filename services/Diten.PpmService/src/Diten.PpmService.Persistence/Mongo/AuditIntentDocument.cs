using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.BuildingBlocks.Eventing;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Mongo;


public sealed class AuditIntentDocument
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid ActorId { get; init; }
    public Guid? CorrelationId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public string Mutation { get; init; } = string.Empty;
    public DateTime OccurredAtUtc { get; init; }
    public DateTime? OutboxEnqueuedAtUtc { get; init; }
    public string? DispatchSignatureScheme { get; init; }
    public string? DispatchKeyId { get; init; }
    public string? DispatchSignature { get; init; }
    public string? DispatchFailureCode { get; init; }
    public DateTime? DispatchUpdatedAtUtc { get; init; }
}
