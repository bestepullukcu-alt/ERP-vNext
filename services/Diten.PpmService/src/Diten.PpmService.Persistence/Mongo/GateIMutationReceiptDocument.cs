using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.BuildingBlocks.Eventing;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Mongo;


public sealed class GateIMutationReceiptDocument
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string OperationId { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public string RequestHash { get; init; } = string.Empty;
    public string ProvenanceHash { get; init; } = string.Empty;
    public Guid AggregateId { get; init; }
    public int AggregateVersion { get; init; }
    public int StatusCode { get; init; }
    public string StableCode { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
