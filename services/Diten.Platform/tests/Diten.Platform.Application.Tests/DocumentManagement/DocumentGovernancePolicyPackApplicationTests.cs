using System.Reflection;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementGovernancePolicyPack;
using Diten.Platform.Application.Features.DocumentManagementRetention;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU31A — governance policy pack API surface + append-only application history. Preview writes nothing;
/// apply is idempotent, never overwrites an existing policy, never mutates subject state, and records one history
/// row per run. History reads are tenant-scoped (cross-tenant resolves to not-found).
/// </summary>
public sealed class DocumentGovernancePolicyPackApplicationTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    private sealed record Harness(
        DocumentGovernancePolicyPackApplicationService Service,
        FakeRetentionRepo Retention,
        FakeGDocPRepo GDocP,
        FakeSignatureRepo Signature,
        FakeHistoryRepo History);

    private static Harness Build(Guid? tenantId)
    {
        var tenant = new FakeTenantContext(tenantId);
        var user = new FakeCurrentUser();
        var r = new FakeRetentionRepo(tenant);
        var g = new FakeGDocPRepo(tenant);
        var s = new FakeSignatureRepo(tenant);
        var h = new FakeHistoryRepo(tenant);
        var seeder = new DocumentGovernancePolicyPackSeeder(r, g, s, tenant, user);
        return new Harness(new DocumentGovernancePolicyPackApplicationService(seeder, h, tenant, user), r, g, s, h);
    }

    // ── 1 / 13 ── preview writes nothing at all
    [Fact]
    public async Task Preview_default_policy_pack_writes_no_application_history_or_policies()
    {
        var hx = Build(TenantA);
        var response = await hx.Service.PreviewAsync("corr");

        Assert.True(response.IsSuccessful);
        Assert.Empty(hx.History.Rows);
        Assert.Empty(hx.Retention.Store);
        Assert.Empty(hx.GDocP.Store);
        Assert.Empty(hx.Signature.Store);
        Assert.Equal(0, hx.Retention.CreateCalls + hx.GDocP.CreateCalls + hx.Signature.CreateCalls);
        Assert.Equal(0, hx.Retention.UpdateCalls + hx.GDocP.UpdateCalls + hx.Signature.UpdateCalls);
    }

    // ── 2 ── preview reports missing / existing / conflict
    [Fact]
    public async Task Preview_reports_missing_existing_and_conflicts()
    {
        var hx = Build(TenantA);
        hx.Retention.Seed(Policy("RETENTION_IDENTIFIER_LEDGER_PERMANENT", RetentionSubjectType.IdentifierAllocationLedger, permanent: true));
        hx.Retention.Seed(Policy("RETENTION_CONTROLLED_COPY_10Y", RetentionSubjectType.ApprovalEvidence, permanent: false)); // diverged

        var model = (await hx.Service.PreviewAsync("corr")).Data!;

        Assert.Equal(42, model.TotalPolicyCount);
        Assert.Equal(20, model.RetentionPolicyCount);
        Assert.Equal(10, model.GDocPPolicyCount);
        Assert.Equal(12, model.SignaturePolicyCount);
        Assert.Equal(1, model.ExistingCount);
        Assert.Equal(1, model.ConflictCount);
        Assert.Equal(40, model.MissingCount);
        Assert.Contains(model.PolicyDefinitions, d => d.PolicyKey == "RETENTION_CONTROLLED_COPY_10Y" && d.Outcome == "Conflict");
    }

    // ── 3 / 4 ── apply records history with the counts
    [Fact]
    public async Task Apply_default_policy_pack_creates_application_history_with_counts()
    {
        var hx = Build(TenantA);
        var model = (await hx.Service.ApplyAsync("corr")).Data!;

        var row = Assert.Single(hx.History.Rows);
        Assert.Equal(row.Id, model.ApplicationId);
        Assert.Equal(TenantA, row.TenantId);
        Assert.Equal(DocumentGovernancePolicyPackManifest.PackKey, row.PackKey);
        Assert.Equal(DocumentGovernancePolicyPackManifest.PackVersion, row.PackVersion);
        Assert.Equal(DocumentGovernancePolicyPackApplicationStatus.Applied, row.ApplicationStatus);
        Assert.Equal(42, row.CreatedPolicyCount);
        Assert.Equal(0, row.SkippedExistingCount);
        Assert.Equal(0, row.ConflictCount);
        Assert.Equal(42, row.CreatedPolicyKeys.Count);
        Assert.Equal(20, row.CreatedRetentionPolicyIds.Count);
        Assert.Equal(10, row.CreatedGDocPPolicyIds.Count);
        Assert.Equal(12, row.CreatedSignaturePolicyIds.Count);
        Assert.False(row.PreviewOnly);
    }

    // ── 5 ── a second apply creates nothing but still writes a NEW append-only history row
    [Fact]
    public async Task Apply_second_run_is_idempotent_and_writes_new_history()
    {
        var hx = Build(TenantA);
        await hx.Service.ApplyAsync("corr");
        var second = (await hx.Service.ApplyAsync("corr")).Data!;

        Assert.Equal(2, hx.History.Rows.Count);
        Assert.Equal(0, second.CreatedPolicyCount);
        Assert.Equal(42, second.SkippedExistingCount);
        Assert.Equal(42, hx.Retention.Store.Count + hx.GDocP.Store.Count + hx.Signature.Store.Count);
        Assert.Equal(42, second.SkippedPolicyKeys.Count);
    }

    // ── 6 ── existing policies are never overwritten
    [Fact]
    public async Task Apply_does_not_overwrite_existing_policies()
    {
        var hx = Build(TenantA);
        var pre = Policy("RETENTION_IDENTIFIER_LEDGER_PERMANENT", RetentionSubjectType.IdentifierAllocationLedger, permanent: true);
        pre.PolicyName = "operator-authored";
        hx.Retention.Seed(pre);

        await hx.Service.ApplyAsync("corr");

        var stored = hx.Retention.Store["RETENTION_IDENTIFIER_LEDGER_PERMANENT"];
        Assert.Equal(pre.Id, stored.Id);
        Assert.Equal("operator-authored", stored.PolicyName);
        Assert.Equal(0, hx.Retention.UpdateCalls);
    }

    // ── 7 ── a conflict downgrades the run to AppliedWithWarnings and is recorded
    [Fact]
    public async Task Apply_conflict_results_applied_with_warnings()
    {
        var hx = Build(TenantA);
        hx.Retention.Seed(Policy("RETENTION_CONTROLLED_COPY_10Y", RetentionSubjectType.ApprovalEvidence, permanent: false));

        var model = (await hx.Service.ApplyAsync("corr")).Data!;
        var row = Assert.Single(hx.History.Rows);

        Assert.Equal(DocumentGovernancePolicyPackApplicationStatus.AppliedWithWarnings, model.Status);
        Assert.Equal(DocumentGovernancePolicyPackApplicationStatus.AppliedWithWarnings, row.ApplicationStatus);
        Assert.Contains("RETENTION_CONTROLLED_COPY_10Y", row.ConflictPolicyKeys);
        Assert.NotEmpty(row.ConflictMessages);
        Assert.NotEmpty(row.WarningMessages);
    }

    // ── 8 ── list returns the tenant's applications
    [Fact]
    public async Task Application_history_list_returns_tenant_applications()
    {
        var hx = Build(TenantA);
        await hx.Service.ApplyAsync("corr");
        await hx.Service.ApplyAsync("corr");

        var list = (await hx.Service.ListApplicationsAsync("corr")).Data!;
        Assert.Equal(2, list.Count);
        Assert.All(list, x => Assert.Equal(DocumentGovernancePolicyPackManifest.PackKey, x.PackKey));
    }

    // ── 9 ── detail returns the full key lists
    [Fact]
    public async Task Application_history_detail_returns_full_key_lists()
    {
        var hx = Build(TenantA);
        var applied = (await hx.Service.ApplyAsync("corr")).Data!;

        var detail = (await hx.Service.GetApplicationAsync(applied.ApplicationId, "corr")).Data!;
        Assert.Equal(applied.ApplicationId, detail.Id);
        Assert.Equal(42, detail.CreatedPolicyKeys.Count);
        Assert.Equal(20, detail.CreatedRetentionPolicyIds.Count);
        Assert.Equal(DocumentGovernancePolicyPackManifest.SopReference, detail.SopReference);
    }

    // ── 10 ── cross-tenant detail is blocked (resolves to not-found, no existence leakage)
    [Fact]
    public async Task Application_history_cross_tenant_blocked()
    {
        var a = Build(TenantA);
        var applied = (await a.Service.ApplyAsync("corr")).Data!;

        var b = Build(TenantB);
        b.History.Rows.AddRange(a.History.Rows); // same physical store, different tenant context
        var response = await b.Service.GetApplicationAsync(applied.ApplicationId, "corr");

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(GovernancePolicyPackReasonCodes.ApplicationNotFound, response.ReasonCode);
    }

    // ── 11 ── unknown id → 404 with the reason code
    [Fact]
    public async Task Application_history_unknown_id_returns_not_found()
    {
        var hx = Build(TenantA);
        var response = await hx.Service.GetApplicationAsync(Guid.NewGuid(), "corr");

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(GovernancePolicyPackReasonCodes.ApplicationNotFound, response.ReasonCode);
    }

    // ── 12 ── unresolved tenant is rejected before anything runs
    [Fact]
    public async Task Apply_and_preview_require_tenant_context()
    {
        var hx = Build(null);

        var apply = await hx.Service.ApplyAsync("corr");
        var preview = await hx.Service.PreviewAsync("corr");

        Assert.False(apply.IsSuccessful);
        Assert.Equal(GovernancePolicyPackReasonCodes.TenantRequired, apply.ReasonCode);
        Assert.False(preview.IsSuccessful);
        Assert.Equal(GovernancePolicyPackReasonCodes.TenantRequired, preview.ReasonCode);
        Assert.Empty(hx.History.Rows);
    }

    // ── 14 ── apply never mutates subject state (no Update on any policy repository)
    [Fact]
    public async Task Apply_does_not_mutate_existing_state()
    {
        var hx = Build(TenantA);
        await hx.Service.ApplyAsync("corr");
        await hx.Service.ApplyAsync("corr");

        Assert.Equal(0, hx.Retention.UpdateCalls);
        Assert.Equal(0, hx.GDocP.UpdateCalls);
        Assert.Equal(0, hx.Signature.UpdateCalls);
    }

    // ── 15/16/17 ── controller permission attribution (FU29A rules; nearest seeded retention keys)
    [Theory]
    [InlineData("default/preview")]
    [InlineData("applications")]
    [InlineData("applications/{id:guid}")]
    public void Controller_read_endpoints_use_retention_view_permission(string route) =>
        Assert.Equal(DocumentRetentionPermissions.RetentionView, KeyByRoute(route));

    [Fact]
    public void Controller_apply_uses_retention_manage_permission() =>
        Assert.Equal(DocumentRetentionPermissions.RetentionManage, KeyByRoute("default/apply"));

    private static string KeyByRoute(string route)
    {
        var controller = typeof(DocumentManagementGovernancePolicyPackController);
        foreach (var m in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var perm = m.GetCustomAttribute<HasPermissionAttribute>();
            if (perm is null) continue;
            var template = m.GetCustomAttributes().OfType<HttpMethodAttribute>().Select(a => a.Template).FirstOrDefault();
            if (string.Equals(template, route, StringComparison.Ordinal)) return perm.Permission;
        }

        throw new InvalidOperationException($"No action with route '{route}' and a [HasPermission] attribute.");
    }

    private static DocumentRetentionPolicy Policy(string key, RetentionSubjectType subject, bool permanent) => new()
    {
        Id = Guid.NewGuid(), TenantId = TenantA, PolicyKey = key, PolicyName = "pre-existing",
        PolicyStatus = RetentionPolicyStatus.Active, SubjectType = subject,
        IsPermanentRetention = permanent, MinimumRetentionYears = permanent ? 0 : 3, RegulatoryBasis = "x"
    };

    // ── fakes ──────────────────────────────────────────────────────────────────────────────────────
    private sealed class FakeTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid TenantId => tenantId ?? Guid.Empty;
        public bool IsResolved => tenantId is not null;
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid t) { }
        public void SetPlatformContext(Guid t) { }
        public void ClearTenant() { }
    }

    private sealed class FakeCurrentUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-3333-3333-3333-333333333333");
        public string? Email => "seed@diten.test";
        public string? DisplayName => "Seed";
        public string ActorName => "seed-runner";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeHistoryRepo(ITenantContext tenant) : IDocumentGovernancePolicyPackApplicationRepository
    {
        public readonly List<DocumentGovernancePolicyPackApplication> Rows = [];

        public Task<DocumentGovernancePolicyPackApplication> CreateAsync(DocumentGovernancePolicyPackApplication a, CancellationToken ct = default)
        {
            Rows.Add(a);
            return Task.FromResult(a);
        }
        // Tenant-scoped like the real TenantRepository ExecutionFilter.
        public Task<DocumentGovernancePolicyPackApplication?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.Id == id && r.TenantId == tenant.TenantId));
        public Task<IReadOnlyList<DocumentGovernancePolicyPackApplication>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGovernancePolicyPackApplication>>(
                Rows.Where(r => r.TenantId == tenant.TenantId).OrderByDescending(r => r.AppliedAt).ToList());
        public Task<DocumentGovernancePolicyPackApplication?> GetLatestByPackKeyAsync(string packKey, CancellationToken ct = default) =>
            Task.FromResult(Rows.Where(r => r.TenantId == tenant.TenantId && r.PackKey == packKey)
                .OrderByDescending(r => r.AppliedAt).FirstOrDefault());
    }

    private sealed class FakeRetentionRepo(ITenantContext tenant) : IDocumentRetentionPolicyRepository
    {
        public readonly Dictionary<string, DocumentRetentionPolicy> Store = new(StringComparer.OrdinalIgnoreCase);
        public int UpdateCalls;
        public int CreateCalls;
        public void Seed(DocumentRetentionPolicy p) => Store[p.PolicyKey] = p;

        public Task<DocumentRetentionPolicy> CreateAsync(DocumentRetentionPolicy p, CancellationToken ct = default)
        { CreateCalls++; Assert.Equal(tenant.TenantId, p.TenantId); Store[p.PolicyKey] = p; return Task.FromResult(p); }
        public Task<DocumentRetentionPolicy?> GetByKeyAsync(string k, CancellationToken ct = default) =>
            Task.FromResult(Store.TryGetValue(k, out var p) ? p : null);
        public Task<DocumentRetentionPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Store.Values.FirstOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<DocumentRetentionPolicy>> GetActiveBySubjectTypeAsync(RetentionSubjectType t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRetentionPolicy>>(Store.Values.Where(p => p.SubjectType == t).ToList());
        public Task<IReadOnlyList<DocumentRetentionPolicy>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRetentionPolicy>>(Store.Values.ToList());
        public Task<bool> UpdateAsync(DocumentRetentionPolicy p, CancellationToken ct = default)
        { UpdateCalls++; Store[p.PolicyKey] = p; return Task.FromResult(true); }
    }

    private sealed class FakeGDocPRepo(ITenantContext tenant) : IDocumentGDocPCorrectionPolicyRepository
    {
        public readonly Dictionary<string, DocumentGDocPCorrectionPolicy> Store = new(StringComparer.OrdinalIgnoreCase);
        public int UpdateCalls;
        public int CreateCalls;

        public Task<DocumentGDocPCorrectionPolicy> CreateAsync(DocumentGDocPCorrectionPolicy p, CancellationToken ct = default)
        { CreateCalls++; Assert.Equal(tenant.TenantId, p.TenantId); Store[p.PolicyKey] = p; return Task.FromResult(p); }
        public Task<DocumentGDocPCorrectionPolicy?> GetByKeyAsync(string k, CancellationToken ct = default) =>
            Task.FromResult(Store.TryGetValue(k, out var p) ? p : null);
        public Task<DocumentGDocPCorrectionPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Store.Values.FirstOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<DocumentGDocPCorrectionPolicy>> GetActiveBySubjectTypeAsync(GDocPSubjectType t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionPolicy>>(Store.Values.Where(p => p.SubjectType == t).ToList());
        public Task<IReadOnlyList<DocumentGDocPCorrectionPolicy>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionPolicy>>(Store.Values.ToList());
        public Task<bool> UpdateAsync(DocumentGDocPCorrectionPolicy p, CancellationToken ct = default)
        { UpdateCalls++; Store[p.PolicyKey] = p; return Task.FromResult(true); }
    }

    private sealed class FakeSignatureRepo(ITenantContext tenant) : IDocumentSignaturePolicyRepository
    {
        public readonly Dictionary<string, DocumentSignaturePolicy> Store = new(StringComparer.OrdinalIgnoreCase);
        public int UpdateCalls;
        public int CreateCalls;

        public Task<DocumentSignaturePolicy> CreateAsync(DocumentSignaturePolicy p, CancellationToken ct = default)
        { CreateCalls++; Assert.Equal(tenant.TenantId, p.TenantId); Store[p.PolicyKey] = p; return Task.FromResult(p); }
        public Task<DocumentSignaturePolicy?> GetByKeyAsync(string k, CancellationToken ct = default) =>
            Task.FromResult(Store.TryGetValue(k, out var p) ? p : null);
        public Task<DocumentSignaturePolicy?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Store.Values.FirstOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<DocumentSignaturePolicy>> GetActiveBySubjectTypeAsync(SignableSubjectType t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignaturePolicy>>(Store.Values.Where(p => p.SignableSubjectType == t).ToList());
        public Task<IReadOnlyList<DocumentSignaturePolicy>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignaturePolicy>>(Store.Values.ToList());
        public Task<bool> UpdateAsync(DocumentSignaturePolicy p, CancellationToken ct = default)
        { UpdateCalls++; Store[p.PolicyKey] = p; return Task.FromResult(true); }
    }
}
