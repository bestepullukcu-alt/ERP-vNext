using System.Text.Json.Serialization;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCapacityPlanRequest
{
    [JsonRequired] public string? Name { get; init; }
    [JsonRequired] public DateOnly HorizonStart { get; init; }
    [JsonRequired] public DateOnly HorizonEnd { get; init; }
    [JsonRequired] public string? DemandPlanId { get; init; }
    [JsonRequired] public string? DemandPlanVersion { get; init; }
    [JsonRequired] public DateTimeOffset SourceCapturedAt { get; init; }
    [JsonRequired] public string? SourceChecksum { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCapacityScenarioRequest
{
    [JsonRequired] public string? Name { get; init; }
    [JsonRequired] public List<CapacityConstraintReference>? ConstraintRefs { get; init; }
    [JsonRequired] public List<CapacityAdjustment>? Adjustments { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvaluateCapacityScenarioRequest
{
    [JsonRequired] public string? EvaluationMode { get; init; }
    [JsonRequired] public List<string>? ResourceRefs { get; init; }
}
public sealed record CapacityProvenance(string DemandPlanId, string DemandPlanVersion, string SourceContract, string SourceContractVersion, DateTimeOffset SourceCapturedAt, string SourceChecksum);
public sealed record CapacityPlanResponse(Guid CapacityPlanId, string Name, DateOnly HorizonStart, DateOnly HorizonEnd, CapacityProvenance Provenance, string Status, DateTimeOffset CreatedAt, string ContractVersion = "v1");
public sealed record CapacityScenarioResponse(Guid ScenarioId, Guid CapacityPlanId, string Name, IReadOnlyList<CapacityConstraintReference> ConstraintRefs, IReadOnlyList<CapacityAdjustment> Adjustments, string Status, DateTimeOffset CreatedAt, string ContractVersion = "v1");
public sealed record CapacityEvaluationResponse(Guid EvaluationId, Guid CapacityPlanId, Guid ScenarioId, string EvaluationMode, string Status, IReadOnlyList<CapacityBottleneck> Bottlenecks, DateTimeOffset SubmittedAt, DateTimeOffset? CompletedAt, string ContractVersion = "v1");
public sealed class CapacityRequestContext
{
    public CapacityScope Scope { get; set; } = new(Guid.Empty, Guid.Empty, Guid.Empty);
    public Guid CorrelationId { get; set; }
    public string IdempotencyKey { get; set; } = "";
}
