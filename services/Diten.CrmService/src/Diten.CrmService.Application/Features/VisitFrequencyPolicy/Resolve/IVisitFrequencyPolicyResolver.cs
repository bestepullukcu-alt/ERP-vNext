using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Domain.Repositories;
using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;

/// <summary>
/// MOD-0165 FU03 read-only resolve seam. This is the <b>single source of truth</b> for frequency resolution: it loads
/// the active candidate policies for the requested target (primary + caller-supplied context ids) and runs the
/// deterministic <see cref="VisitFrequencyResolveEngine"/>. Both the FU03 HTTP endpoint (via its query handler) and
/// in-process consumers (e.g. MOD-0151 FU09B route-candidate readiness) call THIS — no consumer re-implements or
/// copies the engine, and there is no HTTP self-call back through the Gateway. The resolver performs no writes.
/// </summary>
public interface IVisitFrequencyPolicyResolver
{
    Task<VisitFrequencyResolveResult> ResolveAsync(ResolveVisitFrequencyPolicyQuery request, CancellationToken cancellationToken);
}

public sealed class VisitFrequencyPolicyResolver : IVisitFrequencyPolicyResolver
{
    private readonly ITenantContext _tenant;
    private readonly IVisitFrequencyPolicyRepository _repository;

    public VisitFrequencyPolicyResolver(ITenantContext tenant, IVisitFrequencyPolicyRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<VisitFrequencyResolveResult> ResolveAsync(
        ResolveVisitFrequencyPolicyQuery request, CancellationToken cancellationToken)
    {
        // No tenant → resolve against zero candidates → deterministic "unknown" (never a fabricated default).
        if (_tenant.TenantId is not { } tenantId)
        {
            return VisitFrequencyResolveEngine.Resolve(request, Array.Empty<Vfp>(), DateTimeOffset.UtcNow);
        }

        var targetIds = new List<Guid> { request.TargetId };
        AddId(targetIds, request.SegmentId);
        AddId(targetIds, request.TerritoryNodeId);
        AddId(targetIds, request.CampaignId);
        AddId(targetIds, request.ConceptNodeId);
        AddId(targetIds, request.AudienceProfileId);

        var candidates = await _repository.ListActiveByTargetsAsync(tenantId, targetIds, cancellationToken);
        return VisitFrequencyResolveEngine.Resolve(request, candidates, DateTimeOffset.UtcNow);
    }

    private static void AddId(ICollection<Guid> ids, Guid? id)
    {
        if (id is { } value && value != Guid.Empty)
        {
            ids.Add(value);
        }
    }
}
