using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Tests.Territory;

internal static class TenantFactory
{
    public static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }
}

internal sealed class FakeTerritoryModelRepo : ITerritoryModelRepository
{
    public List<TerritoryModel> Items { get; } = new();

    public Task<TerritoryModel?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(m => m.TenantId == tenantId && m.Id == id && !m.IsDeleted));

    public Task<bool> ExistsByCodeAsync(Guid tenantId, string modelCode, Guid? excludeId, CancellationToken ct)
        => Task.FromResult(Items.Any(m => m.TenantId == tenantId && !m.IsDeleted && m.ModelCode == modelCode && m.Id != excludeId));

    public Task<(IReadOnlyList<TerritoryModel> Items, long Total)> ListAsync(
        Guid tenantId, string? search, string? status, int page, int pageSize, CancellationToken ct)
    {
        var q = Items.Where(m => m.TenantId == tenantId && !m.IsDeleted).ToList();
        return Task.FromResult(((IReadOnlyList<TerritoryModel>)q, (long)q.Count));
    }

    public Task<IReadOnlyList<TerritoryModel>> ListActiveAsync(Guid tenantId, Guid excludeId, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<TerritoryModel>)Items
            .Where(m => m.TenantId == tenantId && !m.IsDeleted && m.Id != excludeId
                        && string.Equals(m.Status, "active", StringComparison.OrdinalIgnoreCase)).ToList());

    public Task<IReadOnlyList<TerritoryModel>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<TerritoryModel>)Items
            .Where(m => m.TenantId == tenantId && !m.IsDeleted && ids.Contains(m.Id)).ToList());

    public Task InsertAsync(TerritoryModel model, CancellationToken ct) { Items.Add(model); return Task.CompletedTask; }

    /// <summary>Replaces the stored instance (the FU08 apply path hands back a CLONE, so a no-op fake would hide
    /// whether the write landed).</summary>
    public Task UpdateAsync(TerritoryModel model, CancellationToken ct)
    {
        UpdateCalls++;
        var index = Items.FindIndex(m => m.Id == model.Id);
        if (index >= 0) Items[index] = model;
        return Task.CompletedTask;
    }

    public int UpdateCalls { get; private set; }
}

internal sealed class FakeTerritoryNodeRepo : ITerritoryNodeRepository
{
    public List<TerritoryNode> Items { get; } = new();
    public bool CycleAnswer { get; set; }

    public Task<TerritoryNode?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(n => n.TenantId == tenantId && n.ModelId == modelId && n.Id == id && !n.IsDeleted));

    public Task<bool> ExistsByCodeAsync(Guid tenantId, Guid modelId, string territoryCode, Guid? excludeId, CancellationToken ct)
        => Task.FromResult(Items.Any(n => n.TenantId == tenantId && n.ModelId == modelId && !n.IsDeleted
                                          && n.TerritoryCode == territoryCode && n.Id != excludeId));

    public Task<IReadOnlyList<TerritoryNode>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<TerritoryNode>)Items
            .Where(n => n.TenantId == tenantId && n.ModelId == modelId && !n.IsDeleted).ToList());

    public Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid modelId, Guid nodeId, Guid candidateParentId, CancellationToken ct)
        => Task.FromResult(CycleAnswer);

    public Task InsertAsync(TerritoryNode node, CancellationToken ct) { Items.Add(node); return Task.CompletedTask; }

    public Task UpdateAsync(TerritoryNode node, CancellationToken ct)
    {
        var index = Items.FindIndex(n => n.Id == node.Id);
        if (index >= 0) Items[index] = node;
        return Task.CompletedTask;
    }
}

/// <summary>Fake MOD-0151 reference validator for handler tests. Defaults to a fully-published, ranked set of levels
/// so tests focus on handler logic; individual tests flip a set to "missing" to assert fail-closed behaviour.
///
/// <para><b>Vocabulary-aware since 2026-07-28.</b> This seam used to answer <c>Valid</c> for ANY value of a published
/// set, which let the whole suite stay green while <c>deactivate</c>/<c>archive</c> failed closed in every live tenant
/// (model-status had no <c>inactive</c>, node-status had no <c>archived</c>). It now mirrors the canonical published
/// vocabulary in <c>mod-0151-territory-reference-values.json</c>, so a handler asking for a status the authoring
/// template does not publish fails here exactly as it does live. Sets not listed in <see cref="Vocabulary"/> keep the
/// permissive behaviour (those values are not lifecycle-gating).</para></summary>
internal sealed class FakeTerritoryReferenceValidator : ITerritoryReferenceValidator
{
    private readonly Dictionary<string, int> _levelRanks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["division"] = 10, ["country"] = 20, ["region"] = 30, ["area"] = 40, ["zone"] = 50, ["microzone"] = 60
    };

    /// <summary>The published value codes of the lifecycle-gating sets, mirroring the MOD-0048 authoring template.</summary>
    public Dictionary<string, HashSet<string>> Vocabulary { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [TerritoryReferenceSets.TerritoryModelStatus] =
            new(["draft", "review", "approved", "active", "inactive", "superseded", "archived"], StringComparer.OrdinalIgnoreCase),
        [TerritoryReferenceSets.TerritoryNodeStatus] =
            new(["draft", "active", "inactive", "ended", "archived"], StringComparer.OrdinalIgnoreCase),
        [TerritoryReferenceSets.TerritoryRuleType] =
            new(["geography", "account-list", "account-type", "product-portfolio", "business-scope", "channel", "segment", "manual", "import"],
                StringComparer.OrdinalIgnoreCase),
        [TerritoryReferenceSets.TerritoryConflictPolicy] =
            new(["block", "warn", "priority", "manual-review"], StringComparer.OrdinalIgnoreCase),
        [TerritoryReferenceSets.TerritoryResourceRole] =
            new(["medical-representative", "area-manager", "regional-manager", "division-manager", "product-manager",
                 "business-unit-manager", "hoc", "commercial-manager", "admin", "viewer", "operational-resource"],
                StringComparer.OrdinalIgnoreCase),
        [TerritoryReferenceSets.TerritoryCoverageScope] =
            new(["exact-territory", "territory-subtree", "business-unit", "product-portfolio", "business-scope",
                 "model-wide", "all-business-scopes"], StringComparer.OrdinalIgnoreCase),
        [TerritoryReferenceSets.TerritoryAssignmentStatus] =
            new(["proposed", "active", "ended", "rejected"], StringComparer.OrdinalIgnoreCase),
        [TerritoryReferenceSets.TerritoryAssignmentSource] =
            new(["rule", "manual", "import", "override"], StringComparer.OrdinalIgnoreCase)
    };

    /// <summary>Per-value metadata mirroring the tenant's published attributes. FU04 drives its coverage/primary/
    /// reason rules off these, so a test can model "metadata missing" simply by removing an entry.</summary>
    public Dictionary<(string Set, string Value), Dictionary<string, string>> Metadata { get; } = BuildMetadata();

    private static Dictionary<(string, string), Dictionary<string, string>> BuildMetadata()
    {
        var map = new Dictionary<(string, string), Dictionary<string, string>>();

        void Role(string code, string defaultScope, bool canBePrimary, bool requiresScope, bool sales, bool management)
            => map[(TerritoryReferenceSets.TerritoryResourceRole, code)] = new()
            {
                ["defaultCoverageScope"] = defaultScope,
                ["canBePrimary"] = canBePrimary.ToString().ToLowerInvariant(),
                ["requiresBusinessScope"] = requiresScope.ToString().ToLowerInvariant(),
                ["isSalesRole"] = sales.ToString().ToLowerInvariant(),
                ["isManagementRole"] = management.ToString().ToLowerInvariant()
            };

        Role("medical-representative", "exact-territory", true, false, true, false);
        Role("area-manager", "territory-subtree", true, false, true, true);
        Role("regional-manager", "territory-subtree", true, false, true, true);
        Role("division-manager", "territory-subtree", true, false, true, true);
        Role("product-manager", "product-portfolio", true, true, true, true);
        Role("business-unit-manager", "business-unit", true, true, true, true);
        Role("hoc", "all-business-scopes", false, false, true, true);
        Role("commercial-manager", "territory-subtree", true, true, true, true);
        Role("admin", "model-wide", false, false, false, true);
        Role("viewer", "model-wide", false, false, false, false);
        Role("operational-resource", "business-scope", false, true, false, false);

        void Scope(string code, bool requiresTerritory, bool allowsTerritory, bool requiresScope, bool allowsScope)
            => map[(TerritoryReferenceSets.TerritoryCoverageScope, code)] = new()
            {
                ["requiresTerritoryId"] = requiresTerritory.ToString().ToLowerInvariant(),
                ["allowsTerritoryId"] = allowsTerritory.ToString().ToLowerInvariant(),
                ["requiresBusinessScope"] = requiresScope.ToString().ToLowerInvariant(),
                ["allowsBusinessScope"] = allowsScope.ToString().ToLowerInvariant()
            };

        Scope("exact-territory", true, true, false, true);
        Scope("territory-subtree", true, true, false, true);
        Scope("business-unit", false, false, true, true);
        Scope("product-portfolio", false, false, true, true);
        Scope("business-scope", false, false, true, true);
        Scope("model-wide", false, false, false, false);
        Scope("all-business-scopes", false, false, false, false);

        void Source(string code, bool requiresReason, bool overwritable)
            => map[(TerritoryReferenceSets.TerritoryAssignmentSource, code)] = new()
            {
                ["requiresReason"] = requiresReason.ToString().ToLowerInvariant(),
                ["canBeOverwrittenByRule"] = overwritable.ToString().ToLowerInvariant()
            };

        Source("rule", false, true);
        Source("manual", true, false);
        Source("import", false, false);
        Source("override", true, false);

        void Status(string code, bool activeLike, bool historical, bool allowsMutation)
            => map[(TerritoryReferenceSets.TerritoryAssignmentStatus, code)] = new()
            {
                ["isActiveLike"] = activeLike.ToString().ToLowerInvariant(),
                ["isHistorical"] = historical.ToString().ToLowerInvariant(),
                ["allowsMutation"] = allowsMutation.ToString().ToLowerInvariant()
            };

        Status("proposed", false, false, true);
        Status("active", true, false, false);
        Status("ended", false, true, false);
        Status("rejected", false, true, false);

        return map;
    }

    public HashSet<string> MissingSets { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> InvalidLevels { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Removes a single value from a set's published vocabulary (models a partial/stale publish).</summary>
    public FakeTerritoryReferenceValidator Unpublish(string setCode, string value)
    {
        if (Vocabulary.TryGetValue(setCode, out var values))
        {
            values.Remove(value);
        }

        return this;
    }

    public Task<ReferenceValidationStatus> ValidateValueAsync(string setCode, string value, CancellationToken cancellationToken)
    {
        if (MissingSets.Contains(setCode))
        {
            return Task.FromResult(ReferenceValidationStatus.SetMissing);
        }

        if (Vocabulary.TryGetValue(setCode, out var published) && !published.Contains(value))
        {
            return Task.FromResult(ReferenceValidationStatus.InvalidValue);
        }

        return Task.FromResult(ReferenceValidationStatus.Valid);
    }

    public Task<IReadOnlyDictionary<string, string>?> GetValueMetadataAsync(
        string setCode, string value, CancellationToken cancellationToken)
    {
        if (MissingSets.Contains(setCode))
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>?>(null);
        }

        return Task.FromResult(Metadata.TryGetValue((setCode, value), out var attrs)
            ? (IReadOnlyDictionary<string, string>?)attrs
            : null);
    }

    public Task<LevelRankResult> ResolveLevelRankAsync(string levelCode, CancellationToken cancellationToken)
    {
        if (MissingSets.Contains(TerritoryReferenceSets.TerritoryLevel))
        {
            return Task.FromResult(LevelRankResult.Fail(TerritoryReferenceIssue.SetMissing));
        }

        if (InvalidLevels.Contains(levelCode) || !_levelRanks.TryGetValue(levelCode, out var rank))
        {
            return Task.FromResult(LevelRankResult.Fail(TerritoryReferenceIssue.InvalidValue));
        }

        return Task.FromResult(LevelRankResult.Success(rank));
    }

    /// <summary>Mirrors <see cref="MissingSets"/> so a handler that gates on readiness (FU03 preview) sees the same
    /// "not published" answer as one that gates on a single value.</summary>
    public Task<IReadOnlyList<TerritoryReferenceSetReadiness>> GetReadinessAsync(CancellationToken cancellationToken)
        => Task.FromResult((IReadOnlyList<TerritoryReferenceSetReadiness>)TerritoryReferenceSets.Required
            .Select(d => new TerritoryReferenceSetReadiness(
                d.SetCode, true, !MissingSets.Contains(d.SetCode), d.ExpectedValueCount,
                MissingSets.Contains(d.SetCode) ? 0 : d.ExpectedValueCount, true, Array.Empty<string>()))
            .ToList());
}

internal sealed class FakeTerritoryAssignmentRuleRepo : ITerritoryAssignmentRuleRepository
{
    public List<TerritoryAssignmentRule> Items { get; } = new();

    public Task<TerritoryAssignmentRule?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(r => r.TenantId == tenantId && r.ModelId == modelId && r.Id == id && !r.IsDeleted));

    public Task<bool> ExistsByCodeAsync(Guid tenantId, Guid modelId, string ruleCode, Guid? excludeId, CancellationToken ct)
        => Task.FromResult(Items.Any(r => r.TenantId == tenantId && r.ModelId == modelId && !r.IsDeleted
                                          && string.Equals(r.RuleCode, ruleCode, StringComparison.OrdinalIgnoreCase)
                                          && r.Id != excludeId));

    public Task<IReadOnlyList<TerritoryAssignmentRule>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<TerritoryAssignmentRule>)Items
            .Where(r => r.TenantId == tenantId && r.ModelId == modelId && !r.IsDeleted)
            .OrderBy(r => r.Priority).ThenBy(r => r.RuleCode)
            .ToList());

    public Task InsertAsync(TerritoryAssignmentRule rule, CancellationToken ct) { Items.Add(rule); return Task.CompletedTask; }

    public Task UpdateAsync(TerritoryAssignmentRule rule, CancellationToken ct)
    {
        var index = Items.FindIndex(r => r.Id == rule.Id);
        if (index >= 0) Items[index] = rule;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory account seam for FU03 preview tests. <see cref="Mutations"/> stays empty by construction —
/// the interface has no write member, so a preview that "assigned" something could not compile.</summary>
internal sealed class FakeTerritoryAccountReader : ITerritoryAccountReader
{
    public List<TerritoryAccountSnapshot> Accounts { get; } = new();
    public int ListCallCount { get; private set; }

    public Task<IReadOnlyList<TerritoryAccountSnapshot>> GetByIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<TerritoryAccountSnapshot>)Accounts
            .Where(a => accountIds.Contains(a.AccountId)).ToList());

    public Task<IReadOnlyList<TerritoryAccountSnapshot>> ListForPreviewAsync(Guid tenantId, int limit, CancellationToken ct)
    {
        ListCallCount++;
        return Task.FromResult((IReadOnlyList<TerritoryAccountSnapshot>)Accounts.Take(limit).ToList());
    }

    public Task<long> CountAsync(Guid tenantId, CancellationToken ct) => Task.FromResult((long)Accounts.Count);
}

internal sealed class FakeTerritoryResourceAssignmentRepo : ITerritoryResourceAssignmentRepository
{
    public List<TerritoryResourceAssignment> Items { get; } = new();
    public bool FailLifecycleTransition { get; set; }

    public Task<TerritoryResourceAssignment?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.ModelId == modelId && a.Id == id && !a.IsDeleted));

    public Task<IReadOnlyList<TerritoryResourceAssignment>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<TerritoryResourceAssignment>)Items
            .Where(a => a.TenantId == tenantId && a.ModelId == modelId && !a.IsDeleted)
            .OrderBy(a => a.PositionCode).ThenBy(a => a.ValidFrom)
            .ToList());

    public Task<IReadOnlyList<TerritoryResourceAssignment>> ListByResourceAsync(
        Guid tenantId, string resourceId, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<TerritoryResourceAssignment>)Items
            .Where(a => a.TenantId == tenantId && !a.IsDeleted
                        && string.Equals(a.Resource.ResourceId, resourceId, StringComparison.OrdinalIgnoreCase))
            .ToList());

    public Task InsertAsync(TerritoryResourceAssignment assignment, CancellationToken ct) { Items.Add(assignment); return Task.CompletedTask; }

    public Task UpdateAsync(TerritoryResourceAssignment assignment, CancellationToken ct) => Task.CompletedTask;

    public Task CommitLifecycleTransitionAsync(
        TerritoryResourceAssignment ended, TerritoryResourceAssignment created, CancellationToken ct)
    {
        if (FailLifecycleTransition) throw new InvalidOperationException("Simulated transaction rollback.");
        var index = Items.FindIndex(a => a.Id == ended.Id && a.TenantId == ended.TenantId);
        if (index < 0) throw new InvalidOperationException("Source assignment not found.");
        Items[index] = ended;
        Items.Add(created);
        return Task.CompletedTask;
    }
}

internal sealed class FakeTerritoryActivationUnitOfWork : ITerritoryActivationUnitOfWork
{
    /// <summary>FU04B: the baseline shares the activation boundary, so the fake writes it through the same repo the
    /// read handlers use — a failed commit must therefore leave the repo empty.</summary>
    public FakeTerritoryPlanSnapshotRepo Snapshots { get; } = new();

    public bool FailCommit { get; set; }
    public TerritoryModel? SupersededSourceModel { get; private set; }
    public List<AccountTerritoryAssignment> EndedAccountAssignments { get; } = [];
    public List<AccountTerritoryAssignment> CreatedAccountAssignments { get; } = [];

    public Task CommitAsync(
        TerritoryModel model,
        IReadOnlyCollection<TerritoryNode> nodes,
        IReadOnlyCollection<TerritoryResourceAssignment> resourceAssignments,
        TerritoryResourceAssignmentPlanSnapshot? planSnapshot,
        CancellationToken ct)
    {
        if (FailCommit)
        {
            throw new InvalidOperationException("Simulated activation commit failure.");
        }

        if (planSnapshot is not null)
        {
            Snapshots.Items.Add(planSnapshot);
        }

        return Task.CompletedTask;
    }

    public Task CommitVersionCutoverAsync(
        TerritoryModel targetModel, IReadOnlyCollection<TerritoryNode> targetNodes,
        IReadOnlyCollection<TerritoryResourceAssignment> targetResourceAssignments,
        TerritoryResourceAssignmentPlanSnapshot? planSnapshot, TerritoryModel sourceModel,
        IReadOnlyCollection<TerritoryNode> sourceNodes,
        IReadOnlyCollection<AccountTerritoryAssignment> endedSourceAssignments,
        IReadOnlyCollection<AccountTerritoryAssignment> createdTargetAssignments, CancellationToken ct)
    {
        if (FailCommit) throw new InvalidOperationException("Simulated version cutover failure.");
        SupersededSourceModel = sourceModel;
        EndedAccountAssignments.AddRange(endedSourceAssignments);
        CreatedAccountAssignments.AddRange(createdTargetAssignments);
        if (planSnapshot is not null) Snapshots.Items.Add(planSnapshot);
        return Task.CompletedTask;
    }
}

internal sealed class FakeTerritoryDraftCloneUnitOfWork : ITerritoryDraftCloneUnitOfWork
{
    public TerritoryModel? Model { get; private set; }
    public List<TerritoryNode> Nodes { get; } = [];
    public List<TerritoryAssignmentRule> Rules { get; } = [];

    public Task CommitAsync(TerritoryModel model, IReadOnlyCollection<TerritoryNode> nodes,
        IReadOnlyCollection<TerritoryAssignmentRule> rules, CancellationToken cancellationToken)
    {
        Model = model;
        Nodes.AddRange(nodes);
        Rules.AddRange(rules);
        return Task.CompletedTask;
    }
}

internal sealed class FakeTerritoryPlanSnapshotRepo : ITerritoryResourceAssignmentPlanSnapshotRepository
{
    public List<TerritoryResourceAssignmentPlanSnapshot> Items { get; } = [];

    public Task<TerritoryResourceAssignmentPlanSnapshot?> GetLatestAsync(Guid tenantId, Guid modelId, CancellationToken ct)
        => Task.FromResult(Items
            .Where(s => s.TenantId == tenantId && s.TerritoryModelId == modelId && !s.IsDeleted)
            .OrderByDescending(s => s.SnapshotVersion)
            .FirstOrDefault());

    public Task<IReadOnlyList<TerritoryResourceAssignmentPlanSnapshot>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<TerritoryResourceAssignmentPlanSnapshot>)Items
            .Where(s => s.TenantId == tenantId && s.TerritoryModelId == modelId && !s.IsDeleted)
            .OrderByDescending(s => s.SnapshotVersion)
            .ToList());

    public Task<IReadOnlyList<TerritoryResourceAssignmentPlanSnapshot>> ListByResourceAsync(Guid tenantId, string resourceId, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<TerritoryResourceAssignmentPlanSnapshot>)Items
            .Where(s => s.TenantId == tenantId && !s.IsDeleted
                        && s.Lines.Any(l => string.Equals(l.ResourceId, resourceId, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(s => s.SnapshotVersion)
            .ToList());
}

internal sealed class FakeAccountTerritoryAssignmentRepo : IAccountTerritoryAssignmentRepository
{
    public List<AccountTerritoryAssignment> Items { get; } = [];
    public int InsertManyCalls { get; private set; }
    public Task<AccountTerritoryAssignment?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.TerritoryModelId == modelId && a.Id == id && !a.IsDeleted));
    public Task<IReadOnlyList<AccountTerritoryAssignment>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<AccountTerritoryAssignment>)Items.Where(a => a.TenantId == tenantId && a.TerritoryModelId == modelId && !a.IsDeleted).ToList());
    public Task<IReadOnlyList<AccountTerritoryAssignment>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<AccountTerritoryAssignment>)Items.Where(a => a.TenantId == tenantId && a.AccountId == accountId && !a.IsDeleted).OrderByDescending(a => a.EffectiveFrom).ToList());
    /// <summary>Mirrors the Mongo repository: status filter in the query, effective-window in memory by the caller.</summary>
    public Task<IReadOnlyList<AccountTerritoryAssignment>> ListActiveByAccountIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<AccountTerritoryAssignment>)Items
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && accountIds.Contains(a.AccountId)
                        && string.Equals(a.AssignmentStatus, "active", StringComparison.OrdinalIgnoreCase))
            .ToList());
    public Task InsertManyAsync(IReadOnlyCollection<AccountTerritoryAssignment> assignments, CancellationToken ct)
    {
        InsertManyCalls++; Items.AddRange(assignments); return Task.CompletedTask;
    }
    public Task UpdateManyAsync(IReadOnlyCollection<AccountTerritoryAssignment> assignments, CancellationToken ct) => Task.CompletedTask;
    public Task UpdateAsync(AccountTerritoryAssignment assignment, CancellationToken ct) => Task.CompletedTask;
    public Task CommitApplyAsync(
        IReadOnlyCollection<AccountTerritoryAssignment> ended,
        IReadOnlyCollection<AccountTerritoryAssignment> created,
        CancellationToken ct)
    {
        InsertManyCalls++;
        // Ended records are REPLACED in place, never removed — the "closed, not deleted" contract must be observable
        // in the fake too, otherwise a hard-delete regression would pass the tests.
        foreach (var close in ended)
        {
            var index = Items.FindIndex(a => a.Id == close.Id);
            if (index >= 0) Items[index] = close;
        }

        Items.AddRange(created);
        return Task.CompletedTask;
    }
}

/// <summary>Append-only in-memory import run store (MOD-0151 FU08). Mirrors the interface: no update, no delete.</summary>
internal sealed class FakeTerritoryImportRunRepo : ITerritoryImportRunRepository
{
    public List<TerritoryImportRun> Items { get; } = [];

    public Task InsertAsync(TerritoryImportRun run, CancellationToken ct) { Items.Add(run); return Task.CompletedTask; }

    public Task<IReadOnlyList<TerritoryImportRun>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<TerritoryImportRun>)Items
            .Where(r => r.TenantId == tenantId && r.TerritoryModelId == modelId && !r.IsDeleted)
            .OrderByDescending(r => r.UploadedAt).ToList());

    public Task<IReadOnlyList<TerritoryImportRun>> ListByFileHashAsync(
        Guid tenantId, Guid modelId, string fileHash, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<TerritoryImportRun>)Items
            .Where(r => r.TenantId == tenantId && r.TerritoryModelId == modelId && r.FileHash == fileHash && !r.IsDeleted)
            .OrderByDescending(r => r.UploadedAt).ToList());
}

internal sealed class FakeTerritoryLifecycleAuditPublisher : ITerritoryLifecycleAuditPublisher
{
    public List<(string EventName, TerritoryLifecycleAuditPayload Payload)> Events { get; } = [];

    public Task PublishAsync(string eventName, TerritoryLifecycleAuditPayload payload, CancellationToken cancellationToken)
    {
        Events.Add((eventName, payload));
        return Task.CompletedTask;
    }
}
