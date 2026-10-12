using Diten.SupplyChainService.Domain.Common;
namespace Diten.SupplyChainService.Domain.Features.CapacityPlans;
public sealed record CapacityBottleneck(string ResourceRef, string Period, string RequiredCapacity, string AvailableCapacity, string Shortfall, string UomId);
public sealed class CapacityEvaluation : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid CapacityPlanId { get; set; }
    public Guid ScenarioId { get; set; }
    public string EvaluationMode { get; set; } = "";
    public List<string> ResourceRefs { get; set; } = [];
    public string Status { get; set; } = "Accepted";
    public List<CapacityBottleneck> Bottlenecks { get; set; } = [];
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid CausationId { get; set; }
    public int Attempt { get; set; }
    public long Fence { get; set; }
    public DateTime? LeaseUntil { get; set; }
}
public interface ICapacityLeaseStore
{
    Task<IReadOnlyList<CapacityScope>> FindPendingScopesAsync(CancellationToken ct);
    Task<CapacityEvaluation?> ClaimAsync(CapacityScope scope, CancellationToken ct);
    Task<CapacityEvaluation?> RenewAsync(CapacityScope scope, CapacityEvaluation lease, CancellationToken ct);
    Task<bool> TerminalAsync(CapacityScope scope, Guid id, int version, long fence, string status,
        IReadOnlyList<CapacityBottleneck> bottlenecks, bool expired, CancellationToken ct);
}
