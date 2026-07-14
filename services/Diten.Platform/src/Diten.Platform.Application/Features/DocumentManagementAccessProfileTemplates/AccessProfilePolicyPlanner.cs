using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Diten.Platform.Application.Features.DocumentManagementAccessProfileTemplates;

/// <summary>
/// MOD-0029-FU05 — turns a baseline's register <c>access_profile</c> metadata into Access Matrix policies.
/// Read-only over MOD-0028 (definitions/instances are never mutated). Deterministic and idempotent: a generated
/// policy is keyed by (target, principal=Role, effect); a manual policy on the same key is never overwritten, and a
/// re-run creates no duplicates. Apply runs only on an instantiable (Effective / legacy Published) baseline; every
/// other status is dry-run only. Instance scope produces runtime-enforced CollectionInstance policies; Definition
/// scope is preview/dry-run only (the resolver's CollectionDefinition ancestor is baseline-wide, not per node).
/// </summary>
public sealed class AccessProfilePolicyPlanner
{
    private readonly IBaselineReleaseRepository _baselines;
    private readonly ICollectionDefinitionRepository _definitions;
    private readonly ICollectionInstanceRepository _instances;
    private readonly IDocumentAccessPolicyRepository _policies;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly AccessProfileTemplateOptions _options;

    public AccessProfilePolicyPlanner(
        IBaselineReleaseRepository baselines,
        ICollectionDefinitionRepository definitions,
        ICollectionInstanceRepository instances,
        IDocumentAccessPolicyRepository policies,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        IOptions<AccessProfileTemplateOptions> options)
    {
        _baselines = baselines;
        _definitions = definitions;
        _instances = instances;
        _policies = policies;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _options = options.Value;
    }

    public async Task<Response<AccessProfileTemplateSummary>> RunAsync(
        AccessProfileTemplateRequest request, bool apply, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        var baseline = await _baselines.GetByIdAsync(request.BaselineReleaseId, ct);
        if (baseline is null)
        {
            return Fail("Baseline not found.", 404, AccessProfileTemplateReasonCodes.NotFoundNonLeakage, correlationId);
        }

        // Lifecycle guard: apply only on an instantiable (Effective / legacy Published) baseline; dry-run always ok.
        if (apply && !baseline.Status.IsInstantiable())
        {
            return Fail("Policies can only be applied to an effective baseline.", 400,
                AccessProfileTemplateReasonCodes.BaselineNotEffective, correlationId);
        }

        if (apply && request.Scope == AccessProfileTemplateScope.Definition)
        {
            return Fail("Definition scope is preview/dry-run only; apply requires instance scope.", 400,
                AccessProfileTemplateReasonCodes.ScopeNotApplicable, correlationId);
        }

        var nodes = request.Scope == AccessProfileTemplateScope.Instance
            ? await BuildInstanceNodesAsync(baseline, ct)
            : await BuildDefinitionNodesAsync(baseline, ct);

        var include = ToSet(request.IncludeProfiles);
        var exclude = ToSet(request.ExcludeProfiles);

        var desired = new List<DesiredAccessPolicy>();
        var profileCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var unknownProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingRoles = new HashSet<string>(StringComparer.Ordinal);
        var warnings = new List<string>();

        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.AccessProfile))
            {
                continue;
            }

            var profile = node.AccessProfile.Trim();
            if (include.Count > 0 && !include.Contains(profile))
            {
                continue;
            }

            if (exclude.Contains(profile))
            {
                continue;
            }

            profileCounts[profile] = profileCounts.GetValueOrDefault(profile) + 1;

            var specs = AccessProfileTemplateCatalog.Build(
                profile, node.FolderType, node.FolderName, request.ApplyReadOnlyStatusFolderRules, out var known);
            if (!known)
            {
                unknownProfiles.Add(profile);
                continue;
            }

            foreach (var spec in specs)
            {
                var principalId = _options.Resolve(spec.Role);
                if (principalId is null)
                {
                    missingRoles.Add(spec.Role.ToString());
                    continue;
                }

                desired.Add(new DesiredAccessPolicy(
                    node.TargetType, node.TargetId, principalId, spec.Actions, spec.Effect,
                    profile, spec.Role, node.RegisterFolderId, node.DefinitionId, node.InstanceId));
            }
        }

        // Merge any (target, principal, effect) collisions within the plan into one action set (unique-index safe).
        var merged = desired
            .GroupBy(d => (d.TargetId, d.PrincipalId, d.Effect))
            .Select(g => g.First() with { Actions = g.SelectMany(x => x.Actions).Distinct().ToList() })
            .ToList();

        var reconcile = await ReconcileAsync(merged, baseline.Id, apply, correlationId, ct);

        var profiles = profileCounts
            .Select(kv => new AccessProfileCountModel(kv.Key, kv.Value, AccessProfileTemplateCatalog.IsKnown(kv.Key)))
            .OrderBy(p => p.AccessProfile, StringComparer.Ordinal)
            .ToList();

        if (missingRoles.Count > 0)
        {
            warnings.Add($"{missingRoles.Count} template role(s) have no configured principal mapping and were skipped.");
        }

        if (unknownProfiles.Count > 0)
        {
            warnings.Add($"{unknownProfiles.Count} unknown access profile(s) were skipped (no fallback template applied).");
        }

        var summary = new AccessProfileTemplateSummary(
            baseline.Id,
            baseline.Status.ToString().ToUpperInvariant(),
            request.Scope.ToString(),
            !apply,
            nodes.Count,
            merged.Count,
            reconcile.Created,
            reconcile.Updated,
            reconcile.SkippedManual,
            reconcile.SkippedUnchanged,
            profiles,
            unknownProfiles.OrderBy(p => p, StringComparer.Ordinal).ToList(),
            missingRoles.OrderBy(p => p, StringComparer.Ordinal).ToList(),
            warnings);

        return Response<AccessProfileTemplateSummary>.Success(summary, apply ? 200 : 200, correlationId);
    }

    private async Task<ReconcileResult> ReconcileAsync(
        IReadOnlyList<DesiredAccessPolicy> desired, Guid baselineReleaseId, bool apply, string correlationId, CancellationToken ct)
    {
        var result = new ReconcileResult();
        if (desired.Count == 0)
        {
            return result;
        }

        var targets = desired
            .Select(d => (d.TargetType, d.TargetId))
            .Distinct()
            .ToList();
        var existing = await _policies.GetByTargetsAsync(targets, ct);
        var existingByKey = existing
            .GroupBy(e => Key(e.TargetId, e.PrincipalType, e.PrincipalId, e.Effect), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var tenantId = _tenantContext.TenantId;
        var now = DateTimeOffset.UtcNow;

        foreach (var d in desired)
        {
            var key = Key(d.TargetId, DocumentAccessPrincipalType.Role, d.PrincipalId, d.Effect);
            if (existingByKey.TryGetValue(key, out var match))
            {
                if (match.PolicySource != DocumentAccessPolicySource.AccessProfileTemplate)
                {
                    result.SkippedManual++; // never overwrite a manually authored policy
                    continue;
                }

                if (SameActions(match.Actions, d.Actions))
                {
                    result.SkippedUnchanged++;
                    continue;
                }

                if (apply)
                {
                    match.Actions = d.Actions.Distinct().ToList();
                    match.GeneratedAt = now;
                    match.GeneratedBy = _currentUser.ActorName;
                    match.UpdatedAt = now;
                    match.UpdatedBy = _currentUser.ActorName;
                    await _policies.UpdateAsync(match, ct);
                }

                result.Updated++;
                continue;
            }

            if (apply)
            {
                var id = Guid.NewGuid();
                var entry = new DocumentAccessPolicyEntry
                {
                    Id = id,
                    TenantId = tenantId,
                    AccessPolicyId = id,
                    TargetType = d.TargetType,
                    TargetId = d.TargetId,
                    PrincipalType = DocumentAccessPrincipalType.Role,
                    PrincipalId = d.PrincipalId,
                    Actions = d.Actions.Distinct().ToList(),
                    Effect = d.Effect,
                    InheritFromParent = true,
                    Status = DocumentAccessPolicyStatus.Active,
                    CorrelationId = correlationId,
                    CreatedBy = _currentUser.ActorName,
                    PolicySource = DocumentAccessPolicySource.AccessProfileTemplate,
                    PolicyTemplateKey = d.TemplateKey,
                    SourceBaselineReleaseId = baselineReleaseId,
                    SourceCollectionDefinitionId = d.SourceCollectionDefinitionId,
                    SourceCollectionInstanceId = d.SourceCollectionInstanceId,
                    SourceRegisterFolderId = d.SourceRegisterFolderId,
                    GeneratedAt = now,
                    GeneratedBy = _currentUser.ActorName
                };

                try
                {
                    await _policies.CreateAsync(entry, ct);
                }
                catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    // A concurrently-created (likely manual) policy owns this key; respect it.
                    result.SkippedManual++;
                    continue;
                }
            }

            result.Created++;
        }

        return result;
    }

    private async Task<IReadOnlyList<TemplateNode>> BuildInstanceNodesAsync(BaselineRelease baseline, CancellationToken ct)
    {
        var definitionsByCanonical = (await _definitions.GetByBaselineAsync(baseline.Id, ct))
            .GroupBy(d => d.CanonicalId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var instances = (await _instances.GetAllForTenantAsync(ct))
            .Where(i => i.BaselineReleaseId == baseline.Id && i.InstanceStatus == CollectionInstanceStatus.Active);

        var nodes = new List<TemplateNode>();
        foreach (var instance in instances)
        {
            definitionsByCanonical.TryGetValue(instance.CanonicalId, out var def);
            nodes.Add(new TemplateNode(
                DocumentAccessTargetType.CollectionInstance,
                instance.Id.ToString("D"),
                def?.AccessProfile,
                def?.FolderType,
                def?.Name ?? instance.Name,
                def?.RegisterFolderId,
                def?.Id,
                instance.Id));
        }

        return nodes;
    }

    private async Task<IReadOnlyList<TemplateNode>> BuildDefinitionNodesAsync(BaselineRelease baseline, CancellationToken ct)
    {
        var definitions = await _definitions.GetByBaselineAsync(baseline.Id, ct);
        return definitions
            .Where(d => d.Status == CollectionDefinitionStatus.Active)
            .Select(d => new TemplateNode(
                DocumentAccessTargetType.CollectionDefinition,
                d.Id.ToString("D"),
                d.AccessProfile,
                d.FolderType,
                d.Name,
                d.RegisterFolderId,
                d.Id,
                null))
            .ToList();
    }

    private static HashSet<string> ToSet(IReadOnlyList<string>? values) =>
        new((values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()), StringComparer.OrdinalIgnoreCase);

    private static string Key(string targetId, DocumentAccessPrincipalType principalType, string principalId, DocumentAccessEffect effect) =>
        $"{targetId.Trim()}|{principalType}|{principalId.Trim()}|{effect}".ToLowerInvariant();

    private static bool SameActions(IReadOnlyList<DocumentAccessMatrixAction> a, IReadOnlyList<DocumentAccessMatrixAction> b) =>
        a.Distinct().OrderBy(x => x).SequenceEqual(b.Distinct().OrderBy(x => x));

    private static Response<AccessProfileTemplateSummary> Fail(string error, int status, string reason, string correlationId) =>
        Response<AccessProfileTemplateSummary>.Fail(error, status, reason, correlationId);

    private sealed record TemplateNode(
        DocumentAccessTargetType TargetType,
        string TargetId,
        string? AccessProfile,
        string? FolderType,
        string? FolderName,
        string? RegisterFolderId,
        Guid? DefinitionId,
        Guid? InstanceId);

    private sealed class ReconcileResult
    {
        public int Created;
        public int Updated;
        public int SkippedManual;
        public int SkippedUnchanged;
    }
}
