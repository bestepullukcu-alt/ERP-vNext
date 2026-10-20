using System.Globalization;
using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Approvals;
using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitEvidence;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// WP-VW-W3L (W3-BE-d) — the LAYERED visit evidence rules: business unit > legal entity > tenant (K-L1), field by field
// (K-L2), the time-boxed exception still on top. The legal entity is the rep's Platform primary seat (K-L3, K-L8), the
// business unit the rep's primary territory assignment (K-L4). Missing data or an unreachable Platform skips the layer and
// says why (K-L5) — a visit start is never blocked by it.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Why a layer was (not) taken. <see cref="Resolved"/> = the layer's value is known.</summary>
public static class EvidenceScopeNotes
{
    public const string Resolved = "resolved";

    /// <summary>The rep has no primary seat today in the Platform (no position assignment / no legal entity).</summary>
    public const string NoSeat = "no-seat";

    /// <summary>The rep has no active primary territory assignment in a current model.</summary>
    public const string NoTerritory = "no-territory";

    /// <summary>The rep's primary assignment(s) and model carry no business unit.</summary>
    public const string NoBusinessUnit = "no-business-unit";

    /// <summary>More than one business unit — the layer is skipped, never guessed (K-L4).</summary>
    public const string BusinessUnitAmbiguous = "bu-ambiguous";

    /// <summary>The Platform seat read timed out / failed / refused (K-L5).</summary>
    public const string PlatformUnavailable = "platform-unavailable";

    /// <summary>The tenant has no legal entity layer, so the seat is not read (it could change no rule).</summary>
    public const string NoLayer = "no-layer";

    /// <summary>The whole chain: <see cref="Resolved"/> when no layer was skipped for a reason, else the skipped layers'
    /// reasons (legal entity first) joined by a comma. <see cref="NoLayer"/> is not a reason (nothing was skipped).</summary>
    public static string Combine(string legalEntityNote, string businessUnitNote)
    {
        var reasons = new[] { legalEntityNote, businessUnitNote }
            .Where(n => n is not Resolved and not NoLayer).Distinct(StringComparer.Ordinal).ToList();
        return reasons.Count == 0 ? Resolved : string.Join(",", reasons);
    }
}

// ── the Platform seat (K-L8 contract) ─────────────────────────────────────────────────────────────────────────────

/// <summary>A user's current primary seat as the Platform answers it (K-L8).</summary>
public sealed record OrganizationSeat(
    Guid? PositionId, string? PositionCode, Guid? OrganizationUnitId, string? OrganizationUnitCode, Guid? LegalEntityId,
    DateTimeOffset? EffectiveFrom);

/// <summary><paramref name="Available"/> false = the Platform could not answer (timeout / error / refusal).</summary>
public sealed record OrganizationSeatResult(bool Available, OrganizationSeat? Primary);

/// <summary>A bulk answer: <c>Seats[userId]</c> = the primary seat or null (none); a user in <c>Unavailable</c> could not
/// be read (its batch failed).</summary>
public sealed record OrganizationSeatBulkResult(
    IReadOnlyDictionary<string, OrganizationSeat?> Seats, IReadOnlySet<string> Unavailable);

/// <summary>
/// WP-VW-W3L (K-L8) — reads users' primary seats from the Platform (<c>GET api/platform/organization/seats/{userId}</c>,
/// <c>POST …/seats/lookup</c> ≤ 100) through the Gateway with the caller's token. Never throws into the caller: every
/// failure comes back as "not available" (K-L5).
/// </summary>
public interface IOrganizationSeatReader
{
    Task<OrganizationSeatResult> GetAsync(string userId, CancellationToken cancellationToken);

    /// <summary>One Platform call per 100 users (never one per user).</summary>
    Task<OrganizationSeatBulkResult> LookupAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken);
}

// ── the business unit of a rep (K-L4) ─────────────────────────────────────────────────────────────────────────────

public sealed record BusinessUnitOutcome(string? Code, string Note);

public interface IEvidenceBusinessUnitReader
{
    Task<BusinessUnitOutcome> ReadAsync(Guid tenantId, string resourceId, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>The reps' business units in one pass over the current models (the roster's read).</summary>
    Task<IReadOnlyDictionary<string, BusinessUnitOutcome>> ReadManyAsync(
        Guid tenantId, IReadOnlyCollection<string> resourceIds, DateTimeOffset at, CancellationToken cancellationToken);
}

public sealed class EvidenceBusinessUnitReader : IEvidenceBusinessUnitReader
{
    private const int ModelPageSize = 200;

    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryResourceAssignmentRepository _assignments;

    public EvidenceBusinessUnitReader(ITerritoryModelRepository models, ITerritoryResourceAssignmentRepository assignments)
    {
        _models = models;
        _assignments = assignments;
    }

    public async Task<BusinessUnitOutcome> ReadAsync(Guid tenantId, string resourceId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var primaries = (await _assignments.ListByResourceAsync(tenantId, resourceId, cancellationToken))
            .Where(a => a.TenantId == tenantId && string.Equals(a.Resource.ResourceId, resourceId, StringComparison.Ordinal)
                        && VisitEvidenceRepRoster.IsActivePrimary(a, at))
            .ToList();
        if (primaries.Count == 0)
        {
            return new BusinessUnitOutcome(null, EvidenceScopeNotes.NoTerritory);
        }

        var models = (await _models.ListByIdsAsync(tenantId, primaries.Select(a => a.ModelId).Distinct().ToList(), cancellationToken))
            .Where(m => m.TenantId == tenantId && TerritoryCoverageLifecyclePolicy.IsModelCurrent(m, at))
            .ToDictionary(m => m.Id);
        return Decide(primaries.Where(a => models.ContainsKey(a.ModelId)).Select(a => (a, models[a.ModelId])));
    }

    public async Task<IReadOnlyDictionary<string, BusinessUnitOutcome>> ReadManyAsync(
        Guid tenantId, IReadOnlyCollection<string> resourceIds, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var wanted = resourceIds.Where(r => !string.IsNullOrWhiteSpace(r)).ToHashSet(StringComparer.Ordinal);
        var pairs = new Dictionary<string, List<(TerritoryResourceAssignment, TerritoryModel)>>(StringComparer.Ordinal);
        if (wanted.Count > 0)
        {
            var (models, _) = await _models.ListAsync(tenantId, null, TerritoryCoverageLifecyclePolicy.CurrentModelStatus, 1, ModelPageSize, cancellationToken);
            foreach (var model in models.Where(m => m.TenantId == tenantId && TerritoryCoverageLifecyclePolicy.IsModelCurrent(m, at)))
            {
                foreach (var a in await _assignments.ListByModelAsync(tenantId, model.Id, cancellationToken))
                {
                    if (a.TenantId == tenantId && wanted.Contains(a.Resource.ResourceId) && VisitEvidenceRepRoster.IsActivePrimary(a, at))
                    {
                        (pairs.TryGetValue(a.Resource.ResourceId, out var list) ? list : pairs[a.Resource.ResourceId] = new()).Add((a, model));
                    }
                }
            }
        }

        return wanted.ToDictionary(r => r, r => pairs.TryGetValue(r, out var list)
            ? Decide(list)
            : new BusinessUnitOutcome(null, EvidenceScopeNotes.NoTerritory), StringComparer.Ordinal);
    }

    /// <summary>
    /// Pure (K-L4): every active primary assignment gives its own business units, or its model's when it has none; ONE
    /// distinct unit → that unit; several → ambiguous (skipped, never the first); none → skipped.
    /// </summary>
    public static BusinessUnitOutcome Decide(IEnumerable<(TerritoryResourceAssignment Assignment, TerritoryModel Model)> primaries)
    {
        var list = primaries.ToList();
        if (list.Count == 0)
        {
            return new BusinessUnitOutcome(null, EvidenceScopeNotes.NoTerritory);
        }

        var codes = list
            .SelectMany(p => Codes(p.Assignment.BusinessScopes) is { Count: > 0 } own ? own : Codes(p.Model.BusinessScopes))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return codes.Count switch
        {
            0 => new BusinessUnitOutcome(null, EvidenceScopeNotes.NoBusinessUnit),
            1 => new BusinessUnitOutcome(codes[0], EvidenceScopeNotes.Resolved),
            _ => new BusinessUnitOutcome(null, EvidenceScopeNotes.BusinessUnitAmbiguous)
        };
    }

    private static List<string> Codes(IEnumerable<TerritoryBusinessScope>? scopes)
        => (scopes ?? [])
            .Where(s => string.Equals(s.ScopeType?.Trim(), TerritoryReferenceSets.BusinessUnitScopeType, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(s.ScopeCode))
            .Select(s => s.ScopeCode.Trim())
            .ToList();
}

// ── the scope chain of a rep ──────────────────────────────────────────────────────────────────────────────────────

/// <summary>Resolves a rep's (or many reps') legal entity + business unit. The seat is read only when the tenant has a
/// legal entity layer (<paramref name="readSeat"/>); the bulk read asks the Platform once per 100 reps.</summary>
public interface IEvidenceScopeChainResolver
{
    Task<EvidenceScopeChain> ResolveAsync(Guid tenantId, string resourceId, bool readSeat, DateTimeOffset at, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, EvidenceScopeChain>> ResolveManyAsync(
        Guid tenantId, IReadOnlyCollection<string> resourceIds, bool readSeat, DateTimeOffset at, CancellationToken cancellationToken);
}

public sealed class EvidenceScopeChainResolver : IEvidenceScopeChainResolver
{
    private readonly IEvidenceBusinessUnitReader _businessUnits;
    private readonly IOrganizationSeatReader? _seats;

    public EvidenceScopeChainResolver(IEvidenceBusinessUnitReader businessUnits, IOrganizationSeatReader? seats = null)
    {
        _businessUnits = businessUnits;
        _seats = seats;
    }

    public async Task<EvidenceScopeChain> ResolveAsync(
        Guid tenantId, string resourceId, bool readSeat, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var bu = await _businessUnits.ReadAsync(tenantId, resourceId, at, cancellationToken);
        var le = !readSeat
            ? (null, EvidenceScopeNotes.NoLayer)
            : _seats is null
                ? (null, EvidenceScopeNotes.PlatformUnavailable)
                : LegalEntityOf(await _seats.GetAsync(resourceId, cancellationToken));
        return Chain(le.Item1, le.Item2, bu);
    }

    public async Task<IReadOnlyDictionary<string, EvidenceScopeChain>> ResolveManyAsync(
        Guid tenantId, IReadOnlyCollection<string> resourceIds, bool readSeat, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var ids = resourceIds.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal).ToList();
        var bus = await _businessUnits.ReadManyAsync(tenantId, ids, at, cancellationToken);
        OrganizationSeatBulkResult? seats = null;
        if (readSeat && _seats is not null && ids.Count > 0)
        {
            seats = await _seats.LookupAsync(ids, cancellationToken);
        }

        return ids.ToDictionary(id => id, id =>
        {
            var le = !readSeat
                ? (null, EvidenceScopeNotes.NoLayer)
                : seats is null || seats.Unavailable.Contains(id)
                    ? ((Guid?)null, EvidenceScopeNotes.PlatformUnavailable)
                    : LegalEntityOf(new OrganizationSeatResult(true, seats.Seats.GetValueOrDefault(id)));
            return Chain(le.Item1, le.Item2, bus.GetValueOrDefault(id) ?? new BusinessUnitOutcome(null, EvidenceScopeNotes.NoTerritory));
        }, StringComparer.Ordinal);
    }

    /// <summary>Pure (K-L3 / K-L5): the seat's legal entity, or why there is none.</summary>
    public static (Guid? LegalEntity, string Note) LegalEntityOf(OrganizationSeatResult seat)
        => !seat.Available
            ? (null, EvidenceScopeNotes.PlatformUnavailable)
            : seat.Primary?.LegalEntityId is { } id && id != Guid.Empty
                ? (id, EvidenceScopeNotes.Resolved)
                : (null, EvidenceScopeNotes.NoSeat);

    public static EvidenceScopeChain Chain(Guid? legalEntity, string legalEntityNote, BusinessUnitOutcome bu)
        => new(legalEntity, bu.Code, EvidenceScopeNotes.Combine(legalEntityNote, bu.Note))
        {
            LegalEntityNote = legalEntityNote,
            BusinessUnitNote = bu.Note
        };
}

// ── the layer rules ───────────────────────────────────────────────────────────────────────────────────────────────

public static class VisitEvidenceScopeErrors
{
    public const string Invalid = "visit_evidence_scope_invalid";                       // 400
    public const string NotFound = "visit_evidence_scope_not_found";                    // 404
    public const string BusinessUnitUnknown = "visit_evidence_scope_business_unit_unknown"; // 400
}

/// <summary>A layer write: the fields the layer overrides (null = inherited). The PUT REPLACES the layer's overrides.</summary>
public sealed record VisitEvidencePolicyLayerInput(
    string? LocationMode = null,
    int? RadiusMeters = null,
    int? MaxAccuracyMeters = null,
    string? PhotoMode = null,
    bool? DoctorConfirmation = null,
    bool? DurationConsistency = null,
    int? ShortVisitSeconds = null,
    string? AccountWithoutLocation = null,
    string? QrMode = null,
    string? AntiFraudMode = null,
    int? ExpectedVersion = null);

public static class VisitEvidencePolicyLayerRules
{
    /// <summary>The source a layer gives a field: <c>legal-entity:&lt;id&gt;</c> | <c>business-unit:&lt;code&gt;</c>.</summary>
    public static string SourceOf(VisitEvidencePolicyLayer layer) => layer.ScopeType + ":" + layer.ScopeRef;

    /// <summary>The ten fields of a layer, in the policy's order (<see cref="VisitEvidencePolicyResolver.Values"/>); null =
    /// inherited.</summary>
    public static IReadOnlyList<(string Field, string? Value)> Values(VisitEvidencePolicyLayer l) =>
    [
        ("locationMode", l.LocationMode),
        ("radiusMeters", l.RadiusMeters?.ToString(CultureInfo.InvariantCulture)),
        ("maxAccuracyMeters", l.MaxAccuracyMeters?.ToString(CultureInfo.InvariantCulture)),
        ("photoMode", l.PhotoMode),
        ("doctorConfirmation", l.DoctorConfirmation is { } d ? (d ? "true" : "false") : null),
        ("durationConsistency", l.DurationConsistency is { } c ? (c ? "true" : "false") : null),
        ("shortVisitSeconds", l.ShortVisitSeconds?.ToString(CultureInfo.InvariantCulture)),
        ("accountWithoutLocation", l.AccountWithoutLocation),
        ("qrMode", l.QrMode),
        ("antiFraudMode", l.AntiFraudMode)
    ];

    public static int OverriddenCount(VisitEvidencePolicyLayer l) => Values(l).Count(v => v.Value is not null);

    /// <summary>
    /// K-L2 — applies the layer's OVERRIDDEN fields onto <paramref name="target"/> and records their source; a null field
    /// is left as it is (inherited from the layer below).
    /// </summary>
    public static void ApplyOnto(VisitEvidencePolicy target, VisitEvidencePolicyLayer layer, IDictionary<string, string> sources)
    {
        var source = SourceOf(layer);
        void Take(string field) => sources[field] = source;
        if (layer.LocationMode is { } locationMode) { target.LocationMode = locationMode; Take("locationMode"); }
        if (layer.RadiusMeters is { } radius) { target.RadiusMeters = radius; Take("radiusMeters"); }
        if (layer.MaxAccuracyMeters is { } accuracy) { target.MaxAccuracyMeters = accuracy; Take("maxAccuracyMeters"); }
        if (layer.PhotoMode is { } photoMode) { target.PhotoMode = photoMode; Take("photoMode"); }
        if (layer.DoctorConfirmation is { } doctor) { target.DoctorConfirmation = doctor; Take("doctorConfirmation"); }
        if (layer.DurationConsistency is { } duration) { target.DurationConsistency = duration; Take("durationConsistency"); }
        if (layer.ShortVisitSeconds is { } shortVisit) { target.ShortVisitSeconds = shortVisit; Take("shortVisitSeconds"); }
        if (layer.AccountWithoutLocation is { } noLocation) { target.AccountWithoutLocation = noLocation; Take("accountWithoutLocation"); }
        if (layer.QrMode is { } qr) { target.QrMode = qr; Take("qrMode"); }
        if (layer.AntiFraudMode is { } antiFraud) { target.AntiFraudMode = antiFraud; Take("antiFraudMode"); }
    }

    /// <summary>The layers that apply to a chain, LOWEST first (legal entity, then business unit — K-L1: the business
    /// unit wins). A skipped layer contributes nothing.</summary>
    public static IReadOnlyList<VisitEvidencePolicyLayer> ApplicableLayers(IReadOnlyList<VisitEvidencePolicyLayer> active, EvidenceScopeChain chain)
    {
        var result = new List<VisitEvidencePolicyLayer>();
        if (chain.LegalEntity is { } le
            && active.FirstOrDefault(l => l.IsActive() && l.ScopeType == VisitEvidencePolicyScopeTypes.LegalEntity
                                          && Guid.TryParse(l.ScopeRef, out var id) && id == le) is { } leLayer)
        {
            result.Add(leLayer);
        }

        if (chain.BusinessUnit is { } bu
            && active.FirstOrDefault(l => l.IsActive() && l.ScopeType == VisitEvidencePolicyScopeTypes.BusinessUnit
                                          && string.Equals(l.ScopeRef, bu, StringComparison.OrdinalIgnoreCase)) is { } buLayer)
        {
            result.Add(buLayer);
        }

        return result;
    }

    /// <summary>Validates + copies a write onto the layer (a full replacement of its overrides).</summary>
    public static VisitExecutionRules.Failure? Apply(VisitEvidencePolicyLayer target, VisitEvidencePolicyLayerInput input)
    {
        static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        var bad = false;
        string? Pick(string? value, IReadOnlyList<string> allowed)
        {
            var v = Norm(value);
            if (v is not null && !allowed.Contains(v, StringComparer.Ordinal))
            {
                bad = true;
            }

            return v;
        }

        target.LocationMode = Pick(input.LocationMode, VisitLocationModes.All);
        target.PhotoMode = Pick(input.PhotoMode, VisitPhotoModes.All);
        target.AccountWithoutLocation = Pick(input.AccountWithoutLocation, AccountWithoutLocationModes.All);
        target.QrMode = Pick(input.QrMode, VisitQrModes.All);
        target.AntiFraudMode = Pick(input.AntiFraudMode, VisitAntiFraudModes.All);
        target.RadiusMeters = input.RadiusMeters;
        target.MaxAccuracyMeters = input.MaxAccuracyMeters;
        target.ShortVisitSeconds = input.ShortVisitSeconds;
        target.DoctorConfirmation = input.DoctorConfirmation;
        target.DurationConsistency = input.DurationConsistency;

        if (bad
            || target.RadiusMeters is < VisitEvidencePolicyDefaults.MinRadiusMeters or > VisitEvidencePolicyDefaults.MaxRadiusMeters
            || target.MaxAccuracyMeters is < VisitEvidencePolicyDefaults.MinAccuracyMeters or > VisitEvidencePolicyDefaults.MaxAccuracyMetersBound
            || target.ShortVisitSeconds is < VisitEvidencePolicyDefaults.MinShortVisitSeconds or > VisitEvidencePolicyDefaults.MaxShortVisitSeconds)
        {
            return new VisitExecutionRules.Failure(
                "Policy out of bounds: locationMode required|info, photoMode required|optional, accountWithoutLocation " +
                "allow_flagged|block, radiusMeters 50–5000, maxAccuracyMeters 10–1000, shortVisitSeconds 30–1800, " +
                "qrMode off|optional|required, antiFraudMode off|flag.",
                VisitExecutionErrorCodes.PolicyInvalid, 400);
        }

        if (OverriddenCount(target) == 0)
        {
            return new VisitExecutionRules.Failure(
                "A scope overrides at least one field (to inherit everything, remove the scope).", VisitEvidenceScopeErrors.Invalid, 400);
        }

        return null;
    }

    /// <summary>The history rows of one layer write: <c>created</c> when new, then per field whose override changed — a new
    /// value <c>set</c>, a dropped override <c>cleared</c> (its old value kept). No change ⇒ no row.</summary>
    public static IReadOnlyList<VisitEvidencePolicyChange> HistoryRows(
        Guid tenantId, VisitEvidencePolicyLayer? before, VisitEvidencePolicyLayer after, string? by, DateTimeOffset at)
    {
        var rows = new List<VisitEvidencePolicyChange>();
        if (before is null)
        {
            rows.Add(Row(tenantId, after, by, at, null, null, null, VisitEvidencePolicyChangeKinds.Created));
        }

        var old = before is null ? null : Values(before);
        var now = Values(after);
        for (var i = 0; i < now.Count; i++)
        {
            var was = old?[i].Value;
            if (string.Equals(was, now[i].Value, StringComparison.Ordinal))
            {
                continue;
            }

            rows.Add(Row(tenantId, after, by, at, now[i].Field, was, now[i].Value,
                now[i].Value is null ? VisitEvidencePolicyChangeKinds.Cleared : VisitEvidencePolicyChangeKinds.Set));
        }

        return rows;
    }

    public static VisitEvidencePolicyChange Row(
        Guid tenantId, VisitEvidencePolicyLayer layer, string? by, DateTimeOffset at, string? field, string? oldValue, string? newValue, string kind)
        => new()
        {
            TenantId = tenantId, At = at, By = by, Scope = layer.ScopeType, ScopeRef = layer.ScopeRef, Field = field,
            OldValue = oldValue, NewValue = newValue, Kind = kind
        };

    /// <summary>The scope key from the route: the type must be a layer; a legal entity is a Guid (normalized "D"); a
    /// business unit a non-empty code. The business unit's MOD-0048 check is the handler's (async).</summary>
    public static (string Type, string Ref)? ParseKey(string? scopeType, string? scopeRef)
    {
        var type = scopeType?.Trim().ToLowerInvariant();
        var key = scopeRef?.Trim();
        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(key) || !VisitEvidencePolicyScopeTypes.Layers.Contains(type))
        {
            return null;
        }

        if (type == VisitEvidencePolicyScopeTypes.LegalEntity)
        {
            return Guid.TryParse(key, out var id) && id != Guid.Empty ? (type, id.ToString("D")) : null;
        }

        return key.Length <= 100 ? (type, key) : null;
    }
}

// ── the audit seam ────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>WP-VW-W3L (§7) — the CRM audit seam of the evidence scope writes (structured logging by default, the governed
/// audit append with <c>Crm:Audit:Mode=http</c> — the same publisher as Account / Contact / content).</summary>
public interface IVisitEvidenceAuditPublisher
{
    Task PublishAsync(string eventName, Guid tenantId, string entityType, Guid entityId, int version, string? detail,
        CancellationToken cancellationToken);
}

public static class VisitEvidenceAuditEvents
{
    public const string ScopeWritten = "crm.visit-evidence-policy.scope.written";
    public const string ScopeArchived = "crm.visit-evidence-policy.scope.archived";
    public const string EntityType = "VisitEvidencePolicyLayer";
}

// ── the API: wire shapes ──────────────────────────────────────────────────────────────────────────────────────────

/// <summary>One field of a scope: its override (null = inherited) — for the tenant every field carries its value.</summary>
public sealed record EvidenceScopeFieldDto(string Field, string? Value);

/// <summary>A scope of the list: type, ref, how many fields it overrides, the last change and how many reps it applies
/// to today. <c>RepCountNote</c> = <c>platform-unavailable</c> when some reps' seats could not be read (the count is then
/// a lower bound).</summary>
public sealed record EvidenceScopeDto(
    string ScopeType, string? ScopeRef, int OverriddenFields, int Version, DateTimeOffset? UpdatedAt, string? UpdatedBy,
    int RepCount, string? RepCountNote, bool IsDefault = false);

public sealed record EvidenceScopeListDto(IReadOnlyList<EvidenceScopeDto> Items, int TotalReps,
    IReadOnlyDictionary<string, string>? DisplayNames = null);

public sealed record EvidenceScopeDetailDto(
    string ScopeType, string? ScopeRef, string Status, int Version, DateTimeOffset? UpdatedAt, string? UpdatedBy,
    IReadOnlyList<EvidenceScopeFieldDto> Fields);

public sealed record ListVisitEvidencePolicyScopesQuery : IRequest<Response<EvidenceScopeListDto>>;

public sealed record GetVisitEvidencePolicyScopeQuery(string? ScopeType, string? ScopeRef) : IRequest<Response<EvidenceScopeDetailDto>>;

public sealed record UpsertVisitEvidencePolicyScopeCommand(string? ScopeType, string? ScopeRef, VisitEvidencePolicyLayerInput Input)
    : IRequest<Response<EvidenceScopeDetailDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => VisitEvidenceScopeAudit.Describe(AuditOperation.Update);

    /// <summary>The scope and the NAMES of the fields the request sets (the values are the layer's own history).</summary>
    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        VisitEvidenceScopeAudit.Scope(details, ScopeType, ScopeRef, response).Set("expectedVersion", Input?.ExpectedVersion);
        if (Input is { } input)
        {
            if (input.LocationMode is not null) details.Changed(nameof(input.LocationMode));
            if (input.RadiusMeters is not null) details.Changed(nameof(input.RadiusMeters));
            if (input.MaxAccuracyMeters is not null) details.Changed(nameof(input.MaxAccuracyMeters));
            if (input.PhotoMode is not null) details.Changed(nameof(input.PhotoMode));
            if (input.DoctorConfirmation is not null) details.Changed(nameof(input.DoctorConfirmation));
            if (input.DurationConsistency is not null) details.Changed(nameof(input.DurationConsistency));
            if (input.ShortVisitSeconds is not null) details.Changed(nameof(input.ShortVisitSeconds));
            if (input.AccountWithoutLocation is not null) details.Changed(nameof(input.AccountWithoutLocation));
            if (input.QrMode is not null) details.Changed(nameof(input.QrMode));
            if (input.AntiFraudMode is not null) details.Changed(nameof(input.AntiFraudMode));
        }
    }
}

public sealed record ArchiveVisitEvidencePolicyScopeCommand(string? ScopeType, string? ScopeRef, int? ExpectedVersion)
    : IRequest<Response<EvidenceScopeDetailDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => VisitEvidenceScopeAudit.Describe(AuditOperation.Deactivate);

    public void DescribeAuditDetails(AuditDetails details, object? response)
        => VisitEvidenceScopeAudit.Scope(details, ScopeType, ScopeRef, response).Set("expectedVersion", ExpectedVersion);
}

/// <summary>WP-VW-W6 F8-a (AUD-001 K4, path b) — the evidence scope writes' central audit record (MOD-0155, W3L). A scope
/// is keyed by (type, ref), not by an id: both go in the record.</summary>
public static class VisitEvidenceScopeAudit
{
    public const string SourceModule = "MOD-0155";

    public static AuditDescriptor Describe(AuditOperation operation)
        => new(AuditCategory.Crm, operation, VisitEvidenceAuditEvents.EntityType, SourceModule);

    public static AuditDetails Scope(AuditDetails details, string? scopeType, string? scopeRef, object? response)
    {
        details.Set("scopeType", scopeType).Set("scopeRef", scopeRef);
        if ((response as Response<EvidenceScopeDetailDto>)?.Data is { } scope)
        {
            details.Set("status", scope.Status);
            details.ObjectVersion = scope.Version;
        }

        return details;
    }
}

// ── the API: handlers ─────────────────────────────────────────────────────────────────────────────────────────────

public sealed class VisitEvidencePolicyScopeQueryHandlers :
    IRequestHandler<ListVisitEvidencePolicyScopesQuery, Response<EvidenceScopeListDto>>,
    IRequestHandler<GetVisitEvidencePolicyScopeQuery, Response<EvidenceScopeDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IVisitEvidencePolicyRepository _policies;
    private readonly IVisitEvidencePolicyLayerRepository _layers;
    private readonly IVisitEvidenceRepRoster _reps;
    private readonly IEvidenceScopeChainResolver _chains;
    private readonly TimeProvider _clock;
    private readonly IUserDisplayNameResolver? _names;

    public VisitEvidencePolicyScopeQueryHandlers(ITenantContext tenant, IVisitEvidencePolicyRepository policies,
        IVisitEvidencePolicyLayerRepository layers, IVisitEvidenceRepRoster reps, IEvidenceScopeChainResolver chains,
        TimeProvider? clock = null, IUserDisplayNameResolver? names = null)
    {
        _tenant = tenant;
        _policies = policies;
        _layers = layers;
        _reps = reps;
        _chains = chains;
        _clock = clock ?? TimeProvider.System;
        _names = names;
    }

    public async Task<Response<EvidenceScopeListDto>> Handle(ListVisitEvidencePolicyScopesQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<EvidenceScopeListDto>.Fail("Tenant context is required.", 400);
        }

        var now = _clock.GetUtcNow();
        var stored = await _policies.GetAsync(tenantId, ct);
        var layers = (await _layers.ListActiveAsync(tenantId, ct)).Where(l => l.TenantId == tenantId && l.IsActive()).ToList();
        var reps = await _reps.ListAsync(tenantId, now, ct);
        var readSeat = layers.Any(l => l.ScopeType == VisitEvidencePolicyScopeTypes.LegalEntity);
        // "applies to N reps": every rep's chain, read in bulk (one Platform call per 100 reps, none without a legal entity layer)
        var chains = layers.Count == 0
            ? new Dictionary<string, EvidenceScopeChain>()
            : await _chains.ResolveManyAsync(tenantId, reps.Select(r => r.ResourceId).ToList(), readSeat, now, ct);
        var seatsMissing = chains.Values.Any(c => c.LegalEntityNote == EvidenceScopeNotes.PlatformUnavailable);

        var items = new List<EvidenceScopeDto>
        {
            new(VisitEvidencePolicyScopeTypes.Tenant, null, VisitEvidencePolicyResolver.Values(stored ?? VisitExecutionRules.Defaults(tenantId)).Count,
                stored?.Version ?? 0, stored?.UpdatedAt, stored?.UpdatedBy, reps.Count, null, stored is null)
        };
        foreach (var layer in layers
                     .OrderBy(l => l.ScopeType == VisitEvidencePolicyScopeTypes.LegalEntity ? 0 : 1)
                     .ThenBy(l => l.ScopeRef, StringComparer.OrdinalIgnoreCase))
        {
            var applies = chains.Values.Count(c => VisitEvidencePolicyLayerRules.ApplicableLayers([layer], c).Count == 1);
            var note = layer.ScopeType == VisitEvidencePolicyScopeTypes.LegalEntity && seatsMissing ? EvidenceScopeNotes.PlatformUnavailable : null;
            items.Add(new EvidenceScopeDto(layer.ScopeType, layer.ScopeRef, VisitEvidencePolicyLayerRules.OverriddenCount(layer),
                layer.Version, layer.UpdatedAt ?? layer.CreatedAt, layer.UpdatedBy, applies, note));
        }

        var names = await UserDisplayNames.MapAsync(_names, items.Select(i => i.UpdatedBy), ct);
        return Response<EvidenceScopeListDto>.Success(new EvidenceScopeListDto(items, reps.Count, names));
    }

    public async Task<Response<EvidenceScopeDetailDto>> Handle(GetVisitEvidencePolicyScopeQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<EvidenceScopeDetailDto>.Fail("Tenant context is required.", 400);
        }

        if (string.Equals(request.ScopeType?.Trim(), VisitEvidencePolicyScopeTypes.Tenant, StringComparison.OrdinalIgnoreCase))
        {
            var stored = await _policies.GetAsync(tenantId, ct);
            var policy = stored ?? VisitExecutionRules.Defaults(tenantId);
            return Response<EvidenceScopeDetailDto>.Success(new EvidenceScopeDetailDto(
                VisitEvidencePolicyScopeTypes.Tenant, null, VisitEvidencePolicyLayerStatuses.Active, stored?.Version ?? 0,
                stored?.UpdatedAt, stored?.UpdatedBy,
                VisitEvidencePolicyResolver.Values(policy).Select(v => new EvidenceScopeFieldDto(v.Field, v.Value)).ToList()));
        }

        if (VisitEvidencePolicyLayerRules.ParseKey(request.ScopeType, request.ScopeRef) is not { } key)
        {
            return VisitEvidenceScopeHandlers.InvalidKey<EvidenceScopeDetailDto>();
        }

        var layer = await _layers.GetActiveAsync(tenantId, key.Type, key.Ref, ct);
        return layer is null || layer.TenantId != tenantId
            ? VisitEvidenceScopeHandlers.NotFound<EvidenceScopeDetailDto>()
            : Response<EvidenceScopeDetailDto>.Success(VisitEvidenceScopeHandlers.ToDto(layer));
    }
}

public sealed class VisitEvidenceScopeHandlers :
    IRequestHandler<UpsertVisitEvidencePolicyScopeCommand, Response<EvidenceScopeDetailDto>>,
    IRequestHandler<ArchiveVisitEvidencePolicyScopeCommand, Response<EvidenceScopeDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IVisitEvidencePolicyLayerRepository _layers;
    private readonly IVisitEvidencePolicyChangeRepository _changes;
    private readonly ITerritoryReferenceValidator _references;
    private readonly IVisitEvidenceAuditPublisher _audit;
    private readonly TimeProvider _clock;

    public VisitEvidenceScopeHandlers(ITenantContext tenant, IActorContext actor, IVisitEvidencePolicyLayerRepository layers,
        IVisitEvidencePolicyChangeRepository changes, ITerritoryReferenceValidator references, IVisitEvidenceAuditPublisher audit,
        TimeProvider? clock = null)
    {
        _tenant = tenant;
        _actor = actor;
        _layers = layers;
        _changes = changes;
        _references = references;
        _audit = audit;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>PUT a scope: create it, or replace its overrides at the expected version (409 otherwise). Direct, no
    /// Workflow (K-L6); one history row per changed field; audited.</summary>
    public async Task<Response<EvidenceScopeDetailDto>> Handle(UpsertVisitEvidencePolicyScopeCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<EvidenceScopeDetailDto>.Fail("Tenant context is required.", 400);
        }

        if (VisitEvidencePolicyLayerRules.ParseKey(request.ScopeType, request.ScopeRef) is not { } key)
        {
            return InvalidKey<EvidenceScopeDetailDto>();
        }

        // the business unit is a MOD-0048 published business-unit value (the territory model's rule, fail-closed)
        if (key.Type == VisitEvidencePolicyScopeTypes.BusinessUnit
            && await _references.ValidateValueAsync(TerritoryReferenceSets.BusinessUnitValueSet, key.Ref, ct) != ReferenceValidationStatus.Valid)
        {
            return WorkflowApprovalRules.Fail<EvidenceScopeDetailDto>(VisitEvidenceScopeErrors.BusinessUnitUnknown,
                $"'{key.Ref}' is not a published '{TerritoryReferenceSets.BusinessUnitValueSet}' value.", 400);
        }

        var stored = await _layers.GetActiveAsync(tenantId, key.Type, key.Ref, ct);
        if (stored is not null && stored.TenantId != tenantId)
        {
            stored = null;
        }

        var expected = request.Input.ExpectedVersion;
        if (stored is null ? expected is { } e && e != 0 : expected != stored.Version)
        {
            return Conflict();
        }

        var before = stored is null ? null : Copy(stored);
        var layer = stored ?? new VisitEvidencePolicyLayer
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ScopeType = key.Type, ScopeRef = key.Ref, Status = VisitEvidencePolicyLayerStatuses.Active
        };
        if (VisitEvidencePolicyLayerRules.Apply(layer, request.Input) is { } failure)
        {
            return VisitExecutionAccess.Fail<EvidenceScopeDetailDto>(failure);
        }

        var at = _clock.GetUtcNow();
        layer.UpdatedAt = at;
        layer.UpdatedBy = _actor.ActorName;
        var written = stored is null
            ? await _layers.InsertAsync(layer, ct)
            : await _layers.ReplaceAsync(layer, stored.Version, ct);
        if (!written)
        {
            return Conflict();
        }

        var rows = VisitEvidencePolicyLayerRules.HistoryRows(tenantId, before, layer, _actor.ActorName, at);
        await _changes.InsertManyAsync(rows, ct);
        await _audit.PublishAsync(VisitEvidenceAuditEvents.ScopeWritten, tenantId, VisitEvidenceAuditEvents.EntityType, layer.Id, layer.Version,
            Detail(layer, rows.Count(r => r.Field is not null)), ct);
        return Response<EvidenceScopeDetailDto>.Success(ToDto(layer));
    }

    /// <summary>"Remove the scope" (K-L6): archived, never deleted; the history keeps an <c>archived</c> row; audited. Its
    /// reps fall back to the layer below at once.</summary>
    public async Task<Response<EvidenceScopeDetailDto>> Handle(ArchiveVisitEvidencePolicyScopeCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<EvidenceScopeDetailDto>.Fail("Tenant context is required.", 400);
        }

        if (VisitEvidencePolicyLayerRules.ParseKey(request.ScopeType, request.ScopeRef) is not { } key)
        {
            return InvalidKey<EvidenceScopeDetailDto>();
        }

        var stored = await _layers.GetActiveAsync(tenantId, key.Type, key.Ref, ct);
        if (stored is null || stored.TenantId != tenantId)
        {
            return NotFound<EvidenceScopeDetailDto>();
        }

        if (request.ExpectedVersion is { } expected && expected != stored.Version)
        {
            return Conflict();
        }

        var version = stored.Version;
        var at = _clock.GetUtcNow();
        stored.Status = VisitEvidencePolicyLayerStatuses.Archived;
        stored.ArchivedAt = at;
        stored.ArchivedBy = _actor.ActorName;
        stored.UpdatedAt = at;
        stored.UpdatedBy = _actor.ActorName;
        if (!await _layers.ReplaceAsync(stored, version, ct))
        {
            return Conflict();
        }

        await _changes.InsertManyAsync(
            [VisitEvidencePolicyLayerRules.Row(tenantId, stored, _actor.ActorName, at, null, null, null, VisitEvidencePolicyChangeKinds.Archived)], ct);
        await _audit.PublishAsync(VisitEvidenceAuditEvents.ScopeArchived, tenantId, VisitEvidenceAuditEvents.EntityType, stored.Id, stored.Version,
            Detail(stored, 0), ct);
        return Response<EvidenceScopeDetailDto>.Success(ToDto(stored));
    }

    public static EvidenceScopeDetailDto ToDto(VisitEvidencePolicyLayer l)
        => new(l.ScopeType, l.ScopeRef, l.Status, l.Version, l.UpdatedAt ?? l.CreatedAt, l.UpdatedBy,
            VisitEvidencePolicyLayerRules.Values(l).Select(v => new EvidenceScopeFieldDto(v.Field, v.Value)).ToList());

    /// <summary>The audit detail: the scope key and counts only (no personal data).</summary>
    private static string Detail(VisitEvidencePolicyLayer l, int changedFields)
        => $"scope={l.ScopeType}:{l.ScopeRef};overrides={VisitEvidencePolicyLayerRules.OverriddenCount(l)};changed={changedFields}";

    private static VisitEvidencePolicyLayer Copy(VisitEvidencePolicyLayer l) => new()
    {
        Id = l.Id, TenantId = l.TenantId, ScopeType = l.ScopeType, ScopeRef = l.ScopeRef, Status = l.Status, Version = l.Version,
        LocationMode = l.LocationMode, RadiusMeters = l.RadiusMeters, MaxAccuracyMeters = l.MaxAccuracyMeters, PhotoMode = l.PhotoMode,
        DoctorConfirmation = l.DoctorConfirmation, DurationConsistency = l.DurationConsistency, ShortVisitSeconds = l.ShortVisitSeconds,
        AccountWithoutLocation = l.AccountWithoutLocation, QrMode = l.QrMode, AntiFraudMode = l.AntiFraudMode
    };

    public static Response<T> InvalidKey<T>() => WorkflowApprovalRules.Fail<T>(VisitEvidenceScopeErrors.Invalid,
        "scopeType legal-entity | business-unit; a legal entity scopeRef is a Guid, a business unit scopeRef its code (≤ 100).", 400);

    public static Response<T> NotFound<T>() => WorkflowApprovalRules.Fail<T>(VisitEvidenceScopeErrors.NotFound, "No such evidence scope.", 404);

    private static Response<EvidenceScopeDetailDto> Conflict()
        => Response<EvidenceScopeDetailDto>.Fail(
            new[] { "The scope changed since it was loaded. Reload and try again.", VisitExecutionErrorCodes.ConcurrencyConflict }, 409);
}
