using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.VisitEvidence;

/// <summary>WP-VW-W3 (W3-BE-c) — the new keys (PKS-001; Platform CrmManifestProvider; no seed / grant).</summary>
public static class VisitEvidenceExceptionPermissions
{
    public const string Read = "crm.visit-evidence-exception.read";
    public const string Manage = "crm.visit-evidence-exception.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage };
}

/// <summary>One policy field as it applies to a rep, and where its value came from.</summary>
public sealed record EffectiveEvidenceField(string Field, string Value, string Source, Guid? ExceptionId);

public static class EvidenceFieldSources
{
    public const string Tenant = "tenant";
    public const string Exception = "exception";
}

/// <summary>The layers above the tenant. This round they are never resolved (the seam is ready; the legal entity is the
/// Platform primary seat, the business unit the CRM territory assignment — a later package).</summary>
public sealed record EvidenceScopeChain(Guid? LegalEntity, string? BusinessUnit, string Note)
{
    public static EvidenceScopeChain Unresolved { get; } = new(null, null, "unresolved");
}

/// <summary>
/// The rules that apply to <c>ResourceId</c> at an instant: the tenant policy (stored or the defaults) ⊕ the rep's ACTIVE
/// APPROVED time-boxed exceptions (each relaxes its rules' fields), field by field with its source.
/// </summary>
public sealed record EffectiveEvidencePolicy(
    VisitEvidencePolicy Policy,
    bool IsDefault,
    IReadOnlyList<EffectiveEvidenceField> Fields,
    IReadOnlyList<VisitEvidenceException> ActiveExceptions,
    IReadOnlyList<string> RelaxedRules,
    EvidenceScopeChain ScopeChain)
{
    /// <summary>The exception the start runs under (the earliest-starting active one), or null.</summary>
    public VisitEvidenceException? PrimaryException => ActiveExceptions.OrderBy(e => e.StartDate).ThenBy(e => e.CreatedAt).FirstOrDefault();
}

/// <summary>
/// WP-VW-W3 (W3-BE-c, §2.2) — the ONE place the visit evidence rules of a rep are decided: the visit start and the
/// execution contract read it. Layers: tenant ⊕ active approved exception; the legal entity / business unit layers return
/// "unresolved" until their package (the seam).
/// </summary>
public interface IVisitEvidencePolicyResolver
{
    Task<EffectiveEvidencePolicy> ResolveAsync(Guid tenantId, string? resourceId, DateTimeOffset at, CancellationToken ct);
}

public sealed class VisitEvidencePolicyResolver : IVisitEvidencePolicyResolver
{
    private readonly IVisitEvidencePolicyRepository _policies;
    private readonly IVisitEvidenceExceptionRepository? _exceptions;

    public VisitEvidencePolicyResolver(IVisitEvidencePolicyRepository policies, IVisitEvidenceExceptionRepository? exceptions = null)
    {
        _policies = policies;
        _exceptions = exceptions;
    }

    public async Task<EffectiveEvidencePolicy> ResolveAsync(Guid tenantId, string? resourceId, DateTimeOffset at, CancellationToken ct)
    {
        var stored = await _policies.GetAsync(tenantId, ct);
        var tenantPolicy = stored ?? VisitExecutionRules.Defaults(tenantId);
        var today = DateOnly.FromDateTime(at.UtcDateTime);
        var active = _exceptions is null || string.IsNullOrWhiteSpace(resourceId)
            ? new List<VisitEvidenceException>()
            : (await _exceptions.ListByResourceAsync(tenantId, resourceId, ct))
                .Where(e => e.TenantId == tenantId && !e.IsDeleted
                            && string.Equals(e.ResourceId, resourceId, StringComparison.Ordinal)
                            && e.PhaseOn(today) == VisitEvidenceExceptionPhases.Active)
                .ToList();
        return Combine(tenantPolicy, stored is null, active);
    }

    /// <summary>Pure: the tenant policy with the active exceptions' relaxations applied (a copy — the stored policy is never
    /// changed), field by field with its source.</summary>
    public static EffectiveEvidencePolicy Combine(
        VisitEvidencePolicy tenantPolicy, bool isDefault, IReadOnlyList<VisitEvidenceException> active)
    {
        var effective = Copy(tenantPolicy);
        var bySource = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var exception in active.OrderBy(e => e.StartDate).ThenBy(e => e.CreatedAt))
        {
            foreach (var rule in exception.Rules.Distinct(StringComparer.Ordinal))
            {
                switch (rule)
                {
                    case VisitEvidenceExceptionRules.LocationInfo:
                        effective.LocationMode = VisitLocationModes.Info;
                        bySource.TryAdd("locationMode", exception.Id);
                        break;
                    case VisitEvidenceExceptionRules.PhotoOptional:
                        effective.PhotoMode = VisitPhotoModes.Optional;
                        bySource.TryAdd("photoMode", exception.Id);
                        break;
                    case VisitEvidenceExceptionRules.AccountWithoutLocationAllow:
                        effective.AccountWithoutLocation = AccountWithoutLocationModes.AllowFlagged;
                        bySource.TryAdd("accountWithoutLocation", exception.Id);
                        break;
                    // WP-VW-W3E (W3E-BE-a, K-W3E-9) — required → optional (off / optional stay as they are)
                    case VisitEvidenceExceptionRules.QrOptional:
                        if (string.Equals(effective.QrMode, VisitQrModes.Required, StringComparison.Ordinal))
                        {
                            effective.QrMode = VisitQrModes.Optional;
                            bySource.TryAdd("qrMode", exception.Id);
                        }

                        break;
                }
            }
        }

        var fields = Values(effective)
            .Select(f => bySource.TryGetValue(f.Field, out var id)
                ? new EffectiveEvidenceField(f.Field, f.Value, EvidenceFieldSources.Exception, id)
                : new EffectiveEvidenceField(f.Field, f.Value, EvidenceFieldSources.Tenant, null))
            .ToList();
        var relaxed = active.SelectMany(e => e.Rules).Distinct(StringComparer.Ordinal)
            .Where(r => VisitEvidenceExceptionRules.All.Contains(r)).ToList();
        return new EffectiveEvidencePolicy(effective, isDefault, fields, active, relaxed, EvidenceScopeChain.Unresolved);
    }

    /// <summary>The eight fields as wire name + invariant string value (the history uses the same rendering).</summary>
    public static IReadOnlyList<(string Field, string Value)> Values(VisitEvidencePolicy p) =>
    [
        ("locationMode", p.LocationMode),
        ("radiusMeters", p.RadiusMeters.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ("maxAccuracyMeters", p.MaxAccuracyMeters.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ("photoMode", p.PhotoMode),
        ("doctorConfirmation", p.DoctorConfirmation ? "true" : "false"),
        ("durationConsistency", p.DurationConsistency ? "true" : "false"),
        ("shortVisitSeconds", p.ShortVisitSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ("accountWithoutLocation", p.AccountWithoutLocation),
        ("qrMode", p.QrMode), // WP-VW-W3E (W3E-BE-a)
        ("antiFraudMode", p.AntiFraudMode) // WP-VW-W3E (W3E-BE-b)
    ];

    public static VisitEvidencePolicy Copy(VisitEvidencePolicy p) => new()
    {
        Id = p.Id, TenantId = p.TenantId, Version = p.Version, CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt, UpdatedBy = p.UpdatedBy,
        LocationMode = p.LocationMode, RadiusMeters = p.RadiusMeters, MaxAccuracyMeters = p.MaxAccuracyMeters, PhotoMode = p.PhotoMode,
        DoctorConfirmation = p.DoctorConfirmation, DurationConsistency = p.DurationConsistency, ShortVisitSeconds = p.ShortVisitSeconds,
        AccountWithoutLocation = p.AccountWithoutLocation,
        QrMode = p.QrMode, // WP-VW-W3E (W3E-BE-a)
        AntiFraudMode = p.AntiFraudMode // WP-VW-W3E (W3E-BE-b)
    };
}

/// <summary>A rep (an active primary territory assignment) on the wire.</summary>
public sealed record EvidenceRepDto(string ResourceId, string DisplayName);

/// <summary>
/// WP-VW-W3 (W3-BE-c, §2.2) — the reps the evidence rules apply to: resources with an ACTIVE PRIMARY territory assignment
/// right now (assignment active, primary, its window covering now, on a node, its model operationally current — the
/// <see cref="TerritoryCoverageLifecyclePolicy"/> model gate). Reads only.
/// </summary>
public interface IVisitEvidenceRepRoster
{
    Task<IReadOnlyList<EvidenceRepDto>> ListAsync(Guid tenantId, DateTimeOffset now, CancellationToken ct);
}

public sealed class VisitEvidenceRepRoster : IVisitEvidenceRepRoster
{
    private const int ModelPageSize = 200;

    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryResourceAssignmentRepository _assignments;

    public VisitEvidenceRepRoster(ITerritoryModelRepository models, ITerritoryResourceAssignmentRepository assignments)
    {
        _models = models;
        _assignments = assignments;
    }

    public async Task<IReadOnlyList<EvidenceRepDto>> ListAsync(Guid tenantId, DateTimeOffset now, CancellationToken ct)
    {
        var (models, _) = await _models.ListAsync(tenantId, null, TerritoryCoverageLifecyclePolicy.CurrentModelStatus, 1, ModelPageSize, ct);
        var reps = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var model in models.Where(m => TerritoryCoverageLifecyclePolicy.IsModelCurrent(m, now)))
        {
            foreach (var a in await _assignments.ListByModelAsync(tenantId, model.Id, ct))
            {
                if (IsActivePrimary(a, now) && !reps.ContainsKey(a.Resource.ResourceId))
                {
                    reps[a.Resource.ResourceId] = string.IsNullOrWhiteSpace(a.Resource.DisplayName) ? a.Resource.ResourceId : a.Resource.DisplayName.Trim();
                }
            }
        }

        return reps.Select(r => new EvidenceRepDto(r.Key, r.Value))
            .OrderBy(r => r.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static bool IsActivePrimary(TerritoryResourceAssignment a, DateTimeOffset now)
        => !a.IsDeleted && a.IsPrimary
           && a.TerritoryId is { } t && t != Guid.Empty
           && !string.IsNullOrWhiteSpace(a.Resource.ResourceId)
           && string.Equals(a.Status, TerritoryCoverageLifecyclePolicy.CurrentAssignmentStatus, StringComparison.OrdinalIgnoreCase)
           && a.ValidFrom <= now && (a.ValidTo is null || a.ValidTo >= now);
}
