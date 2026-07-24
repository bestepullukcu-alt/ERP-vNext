namespace Diten.Platform.Application.Features.WorkAggregation.Providers;

// WC-1 (DCP-004) — the extension seam WC-5 uses. A provider surfaces the current actor's
// work items already projected into the canonical WorkItemProjectionDto shape. In WC-1 exactly ONE provider
// is bound (the MOD-0023 approval provider); the aggregation handler iterates a provider COLLECTION so WC-5
// can add providers additively, without rewriting the projection.
//
// READ-ONLY: a provider must never write business state. Each provider declares its ProviderCode and
// ProviderContractVersion; the handler skips a provider whose contract version it does not support
// (charter OD-WC-04) rather than mis-projecting.
public interface IWorkItemProvider
{
    string ProviderCode { get; }

    string ProviderContractVersion { get; }

    Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(WorkItemActor actor, CancellationToken ct = default);
}
