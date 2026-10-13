using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Approvals;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitEvidence;

// WP-VW-W3 (W3-BE-c, §2.2) — the evidence settings page's reads: the change history, the effective rules of one rep and
// the rep search. Read with crm.visit-evidence-policy.read OR .manage (the controller decides).

public sealed record VisitEvidencePolicyChangeDto(
    DateTimeOffset At, string? By, string Scope, string? Field, string? OldValue, string? NewValue, string Kind);

public sealed record VisitEvidencePolicyHistoryDto(IReadOnlyList<VisitEvidencePolicyChangeDto> Items, long Total, int Page, int PageSize,
    // WP-VW-W3-FIX (F5, additive) — user id ("D") → display name for the page's "by" values.
    IReadOnlyDictionary<string, string>? DisplayNames = null);

public sealed record EffectiveEvidenceFieldDto(string Field, string Value, string Source, Guid? ExceptionId);

public sealed record EvidenceScopeChainDto(Guid? LegalEntity, string? BusinessUnit, string Note);

public sealed record VisitEvidenceEffectiveDto(
    string ResourceId,
    IReadOnlyList<EffectiveEvidenceFieldDto> Fields,
    VisitEvidenceExceptionDto? ActiveException,
    IReadOnlyList<string> RelaxedRules,
    EvidenceScopeChainDto ScopeChain);

public sealed record GetVisitEvidencePolicyHistoryQuery(int? Page = null, int? PageSize = null)
    : IRequest<Response<VisitEvidencePolicyHistoryDto>>;

public sealed record GetVisitEvidenceEffectiveQuery(string? ResourceId) : IRequest<Response<VisitEvidenceEffectiveDto>>;

public sealed record ListVisitEvidenceRepsQuery(string? Q) : IRequest<Response<IReadOnlyList<EvidenceRepDto>>>;

public static class VisitEvidencePolicyHistory
{
    /// <summary>
    /// The history rows of one PUT: a <c>created</c> row when the tenant had no document (then every field that differs
    /// from the defaults is a <c>set</c> row), else one <c>set</c> row per field whose value changed. No change ⇒ no row.
    /// </summary>
    public static IReadOnlyList<VisitEvidencePolicyChange> Rows(
        Guid tenantId, VisitEvidencePolicy before, VisitEvidencePolicy after, bool created, string? by, DateTimeOffset at)
    {
        var rows = new List<VisitEvidencePolicyChange>();
        if (created)
        {
            rows.Add(new VisitEvidencePolicyChange { TenantId = tenantId, At = at, By = by, Kind = VisitEvidencePolicyChangeKinds.Created });
        }

        var old = VisitEvidencePolicyResolver.Values(before);
        var now = VisitEvidencePolicyResolver.Values(after);
        for (var i = 0; i < now.Count; i++)
        {
            if (!string.Equals(old[i].Value, now[i].Value, StringComparison.Ordinal))
            {
                rows.Add(new VisitEvidencePolicyChange
                {
                    TenantId = tenantId, At = at, By = by, Field = now[i].Field, OldValue = old[i].Value, NewValue = now[i].Value,
                    Kind = VisitEvidencePolicyChangeKinds.Set
                });
            }
        }

        return rows;
    }
}

public sealed class VisitEvidencePolicyReadHandlers :
    IRequestHandler<GetVisitEvidencePolicyHistoryQuery, Response<VisitEvidencePolicyHistoryDto>>,
    IRequestHandler<GetVisitEvidenceEffectiveQuery, Response<VisitEvidenceEffectiveDto>>,
    IRequestHandler<ListVisitEvidenceRepsQuery, Response<IReadOnlyList<EvidenceRepDto>>>
{
    public const int MaxReps = 20;

    private readonly ITenantContext _tenant;
    private readonly IVisitEvidencePolicyChangeRepository _changes;
    private readonly IVisitEvidencePolicyResolver _resolver;
    private readonly IVisitEvidenceRepRoster _reps;
    private readonly TimeProvider _clock;
    private readonly IUserDisplayNameResolver? _names;

    public VisitEvidencePolicyReadHandlers(ITenantContext tenant, IVisitEvidencePolicyChangeRepository changes,
        IVisitEvidencePolicyResolver resolver, IVisitEvidenceRepRoster reps, TimeProvider? clock = null,
        IUserDisplayNameResolver? names = null)
    {
        _tenant = tenant;
        _changes = changes;
        _resolver = resolver;
        _reps = reps;
        _clock = clock ?? TimeProvider.System;
        _names = names;
    }

    public async Task<Response<VisitEvidencePolicyHistoryDto>> Handle(GetVisitEvidencePolicyHistoryQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitEvidencePolicyHistoryDto>.Fail("Tenant context is required.", 400);
        }

        var page = Math.Max(1, request.Page ?? 1);
        var size = Math.Clamp(request.PageSize ?? 50, 1, 200);
        var (items, total) = await _changes.ListAsync(tenantId, (page - 1) * size, size, ct);
        var names = await UserDisplayNames.MapAsync(_names, items.Select(c => c.By), ct);
        return Response<VisitEvidencePolicyHistoryDto>.Success(new VisitEvidencePolicyHistoryDto(
            items.Select(c => new VisitEvidencePolicyChangeDto(c.At, c.By, c.Scope, c.Field, c.OldValue, c.NewValue, c.Kind)).ToList(),
            total, page, size, names));
    }

    public async Task<Response<VisitEvidenceEffectiveDto>> Handle(GetVisitEvidenceEffectiveQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitEvidenceEffectiveDto>.Fail("Tenant context is required.", 400);
        }

        var resourceId = request.ResourceId?.Trim();
        if (string.IsNullOrEmpty(resourceId))
        {
            return WorkflowApprovalRules.Fail<VisitEvidenceEffectiveDto>(VisitEvidenceExceptionErrors.Invalid, "resourceId is required.", 400);
        }

        var now = _clock.GetUtcNow();
        var effective = await _resolver.ResolveAsync(tenantId, resourceId, now, ct);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var active = effective.PrimaryException;
        var none = new VisitEvidenceExceptionAbilities(false, false, false, false, false, false);
        return Response<VisitEvidenceEffectiveDto>.Success(new VisitEvidenceEffectiveDto(
            resourceId,
            effective.Fields.Select(f => new EffectiveEvidenceFieldDto(f.Field, f.Value, f.Source, f.ExceptionId)).ToList(),
            active is null ? null : VisitEvidenceExceptionDto.From(active, today, none),
            effective.RelaxedRules,
            new EvidenceScopeChainDto(effective.ScopeChain.LegalEntity, effective.ScopeChain.BusinessUnit, effective.ScopeChain.Note)));
    }

    public async Task<Response<IReadOnlyList<EvidenceRepDto>>> Handle(ListVisitEvidenceRepsQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<EvidenceRepDto>>.Fail("Tenant context is required.", 400);
        }

        var q = request.Q?.Trim();
        var reps = (await _reps.ListAsync(tenantId, _clock.GetUtcNow(), ct))
            .Where(r => string.IsNullOrEmpty(q) || r.DisplayName.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                                                 || r.ResourceId.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(MaxReps)
            .ToList();
        return Response<IReadOnlyList<EvidenceRepDto>>.Success(reps);
    }
}
