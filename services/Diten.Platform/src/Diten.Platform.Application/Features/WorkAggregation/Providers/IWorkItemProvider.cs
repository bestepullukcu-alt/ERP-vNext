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

    /// <summary>
    /// Every permission key this provider consults through <see cref="WorkItemActor.Has"/> when deciding whether
    /// an action is enabled.
    ///
    /// <para>The API layer evaluates ONLY the keys collected here against the caller's claims and hands the granted
    /// set to the read query. A key the provider checks but does not declare is therefore never evaluated, so
    /// <c>actor.Has(key)</c> silently returns false and the action is projected as PERMISSION_DENIED even for a
    /// caller who genuinely holds it — which is exactly what happened when the key list lived hardcoded in
    /// WorkItemsController and MOD-0024 was added. Declaring it here keeps the two in step by construction, so a
    /// third provider cannot repeat the mistake.</para>
    /// </summary>
    IReadOnlyCollection<string> RequiredActionPermissions { get; }

    Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(WorkItemActor actor, CancellationToken ct = default);
}
