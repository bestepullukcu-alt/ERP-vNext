using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Analysis;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Handlers;

/// <summary>
/// WP-FREQ-DET-A — read-only DETAY ANALİZ handler. It loads the policy (tenant-scoped), computes its target impact via
/// the <see cref="IVisitFrequencyTargetImpactCounter"/> seam, normalises the required-visit count to one quarter, and
/// produces the conflict block by REUSING the FU03 resolve engine (<see cref="IVisitFrequencyPolicyResolver"/>) for the
/// policy's own target + context. It performs NO writes and changes no resolve/CRUD behaviour.
/// </summary>
public sealed class GetVisitFrequencyPolicyAnalysisHandler
    : IRequestHandler<GetVisitFrequencyPolicyAnalysisQuery, Response<VisitFrequencyPolicyAnalysisDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IVisitFrequencyPolicyRepository _repository;
    private readonly IVisitFrequencyPolicyResolver _resolver;
    private readonly IVisitFrequencyTargetImpactCounter _impactCounter;
    private readonly ICyclePeriodRepository _cyclePeriods;
    private readonly IUserDisplayNameResolver _userDisplayNames;

    private static readonly IReadOnlyDictionary<Guid, string> EmptyNames = new Dictionary<Guid, string>();

    public GetVisitFrequencyPolicyAnalysisHandler(
        ITenantContext tenant,
        IVisitFrequencyPolicyRepository repository,
        IVisitFrequencyPolicyResolver resolver,
        IVisitFrequencyTargetImpactCounter impactCounter,
        ICyclePeriodRepository cyclePeriods,
        IUserDisplayNameResolver userDisplayNames)
    {
        _tenant = tenant;
        _repository = repository;
        _resolver = resolver;
        _impactCounter = impactCounter;
        _cyclePeriods = cyclePeriods;
        _userDisplayNames = userDisplayNames;
    }

    public async Task<Response<VisitFrequencyPolicyAnalysisDto>> Handle(
        GetVisitFrequencyPolicyAnalysisQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitFrequencyPolicyAnalysisDto>.Fail("Tenant context is required.", 400);
        }

        var policy = await _repository.GetByIdAsync(tenantId, request.PolicyId, cancellationToken);
        if (policy is null)
        {
            return Response<VisitFrequencyPolicyAnalysisDto>.Fail("Visit frequency policy not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        var impact = await BuildImpactAsync(tenantId, policy, now, cancellationToken);
        var conflicts = await BuildConflictsAsync(policy, cancellationToken);
        var timeline = await BuildTimelineAsync(tenantId, policy, cancellationToken);

        return Response<VisitFrequencyPolicyAnalysisDto>.Success(
            new VisitFrequencyPolicyAnalysisDto(policy.Id, impact, conflicts, timeline));
    }

    /// <summary>
    /// WP-FREQ-DET-C — the DURUM AKIŞI timeline. Real embedded <see cref="Vfp.Events"/> are projected in chronological
    /// order; a policy that predates the trail (empty Events) is BACKFILLED from the CreatedAt / ArchivedAt stamps
    /// (created + archived only — never a fabricated weight change). A derived "next-eval" future entry is appended when
    /// it can be derived (cycle-period end → EffectiveTo → omitted). This is a read-side projection; it writes nothing.
    /// </summary>
    private async Task<IReadOnlyList<VisitFrequencyPolicyTimelineEntryDto>> BuildTimelineAsync(
        Guid tenantId, Vfp policy, CancellationToken cancellationToken)
    {
        var entries = new List<VisitFrequencyPolicyTimelineEntryDto>();

        if (policy.Events.Count > 0)
        {
            entries.AddRange(policy.Events
                .OrderBy(e => e.At)
                .Select(e => new VisitFrequencyPolicyTimelineEntryDto(
                    e.Type, e.At, e.By, e.FromValue, e.ToValue, IsFuture: false)));
        }
        else
        {
            // Backfill from timestamps only — the created + (optional) archived point events. There is no timestamp for
            // a historic weight or status change, so those are never invented for a pre-trail policy.
            entries.Add(new VisitFrequencyPolicyTimelineEntryDto(
                FrequencyPolicyEventType.Created, policy.CreatedAt, policy.CreatedBy, null, null, IsFuture: false));
            if (policy.ArchivedAt is { } archivedAt)
            {
                entries.Add(new VisitFrequencyPolicyTimelineEntryDto(
                    FrequencyPolicyEventType.Archived, archivedAt, policy.ArchivedBy, null, null, IsFuture: false));
            }
        }

        var nextEvalAt = await DeriveNextEvaluationAsync(tenantId, policy, cancellationToken);
        if (nextEvalAt is { } at)
        {
            entries.Add(new VisitFrequencyPolicyTimelineEntryDto(
                FrequencyPolicyEventType.NextEval, at, By: null, FromValue: null, ToValue: null, IsFuture: true));
        }

        return await ResolveActorNamesAsync(entries, cancellationToken);
    }

    /// <summary>
    /// WP-FREQ-DET-E — turns each timeline entry's actor id (event.By / CreatedBy / ArchivedBy) into a DISPLAY NAME so the
    /// DURUM AKIŞI reads "… · M. Arslan" instead of a raw GUID. Every GUID-shaped actor across the whole timeline is
    /// resolved in ONE bulk AuthService call (the GetSegmentByIdHandler pattern, REUSING <see cref="IUserDisplayNameResolver"/>
    /// — no new client). Fail-closed and non-fabricating: an id that cannot be resolved degrades to a short id, and a By
    /// that is not a GUID (already a human label / legacy string) is left untouched. This changes ONLY the actor label —
    /// impact, conflicts and the next-eval derivation are computed elsewhere and are not affected.
    /// </summary>
    private async Task<IReadOnlyList<VisitFrequencyPolicyTimelineEntryDto>> ResolveActorNamesAsync(
        List<VisitFrequencyPolicyTimelineEntryDto> entries, CancellationToken cancellationToken)
    {
        // Distinct, non-empty, GUID-shaped actor ids only — the ids the resolver can actually answer for.
        var ids = entries
            .Select(e => e.By)
            .Where(by => Guid.TryParse(by, out var id) && id != Guid.Empty)
            .Select(by => Guid.Parse(by!))
            .Distinct()
            .ToList();

        // ONE bulk round trip for every actor; the resolver is fail-closed (empty map on any failure). No call for none.
        var names = ids.Count == 0
            ? EmptyNames
            : await _userDisplayNames.ResolveAsync(ids, cancellationToken);

        return entries
            .Select(e => e.By is null ? e : e with { By = ActorLabel(e.By, names) })
            .ToList();
    }

    /// <summary>Resolved id ⇒ display name; unresolved GUID ⇒ short id (never a fabricated name); a non-GUID By is a label
    /// already and is returned as-is.</summary>
    private static string? ActorLabel(string? actorId, IReadOnlyDictionary<Guid, string> names)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return actorId;
        }

        if (!Guid.TryParse(actorId, out var id) || id == Guid.Empty)
        {
            return actorId;
        }

        return names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : ShortId(actorId);
    }

    private static string ShortId(string id) => id.Length > 8 ? id[..8] + "…" : id;

    /// <summary>Derives the "Sonraki değerlendirme" instant WITHOUT inventing one: the cycle-period end when the policy
    /// is cycle-scoped and the period is readable, else the policy's EffectiveTo, else null (the row is omitted). An
    /// archived policy has no next evaluation.</summary>
    private async Task<DateTimeOffset?> DeriveNextEvaluationAsync(
        Guid tenantId, Vfp policy, CancellationToken cancellationToken)
    {
        if (string.Equals(policy.Status, FrequencyPolicyStatus.Archived, StringComparison.Ordinal))
        {
            return null;
        }

        if (policy.CyclePeriodId is { } cyclePeriodId)
        {
            var period = await _cyclePeriods.GetByIdAsync(tenantId, cyclePeriodId, cancellationToken);
            if (period is not null)
            {
                return period.EndDate;
            }
        }

        return policy.EffectiveTo;
    }

    private async Task<VisitFrequencyPolicyImpactDto> BuildImpactAsync(
        Guid tenantId, Vfp policy, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var impact = await _impactCounter.CountAsync(tenantId, policy, now, cancellationToken);

        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(impact.Note))
        {
            notes.Add(impact.Note!);
        }

        int? plannedVisitsPerQuarter = null;
        if (impact.Computable && impact.Count is { } count)
        {
            var perTargetPerQuarter = QuarterMultiplier(policy.PeriodType);
            if (perTargetPerQuarter is { } multiplier)
            {
                plannedVisitsPerQuarter = count * policy.RequiredVisitCount * multiplier;
            }
            else
            {
                // cycle / campaign-period / custom have no fixed quarter length: report the raw product and say so.
                plannedVisitsPerQuarter = count * policy.RequiredVisitCount;
                notes.Add("dönem bazlı (cycle/campaign-period/custom); çeyreğe normalize edilemedi");
            }
        }

        var note = notes.Count == 0 ? null : string.Join("; ", notes);
        return new VisitFrequencyPolicyImpactDto(
            impact.Count, impact.Computable, plannedVisitsPerQuarter, note);
    }

    /// <summary>Visits-per-quarter multiplier for a normalisable period, or null when the period is not normalisable
    /// (cycle / campaign-period / custom). day ≈ 91 (13 weeks × 7), week = 13, month = 3, quarter = 1.</summary>
    private static int? QuarterMultiplier(string periodType) => FrequencyPeriodType.Normalize(periodType) switch
    {
        FrequencyPeriodType.Quarter => 1,
        FrequencyPeriodType.Month => 3,
        FrequencyPeriodType.Week => 13,
        FrequencyPeriodType.Day => 91,
        _ => null
    };

    private async Task<VisitFrequencyPolicyConflictsDto> BuildConflictsAsync(Vfp policy, CancellationToken cancellationToken)
    {
        // Resolve the policy's OWN target + stored context. EffectiveAt is left null (now) so the block answers "which
        // active policy governs this target right now?". This REUSES the FU03 resolver — no new resolution logic.
        var query = new ResolveVisitFrequencyPolicyQuery(
            policy.TargetType,
            policy.TargetId,
            EffectiveAt: null,
            BusinessUnit: policy.BusinessUnit,
            TerritoryNodeId: policy.TerritoryNodeId,
            CampaignId: policy.CampaignId,
            SegmentId: policy.SegmentId,
            BrandId: policy.BrandId,
            ProductId: policy.ProductId,
            ConceptNodeId: null,
            AudienceProfileId: null,
            IncludeDiagnostics: true);

        var result = await _resolver.ResolveAsync(query, cancellationToken);

        var candidates = result.CandidatePolicies
            .Select(c => new VisitFrequencyPolicyConflictCandidateDto(
                c.PolicyId,
                c.PolicyCode,
                c.PolicyName,
                $"{c.RequiredVisitCount}×/{c.PeriodType}",
                c.Priority,
                c.Selected,
                c.Reason))
            .ToList();

        return new VisitFrequencyPolicyConflictsDto(
            result.SelectedFrequencyPolicyId, result.FrequencyStatus, candidates);
    }
}
