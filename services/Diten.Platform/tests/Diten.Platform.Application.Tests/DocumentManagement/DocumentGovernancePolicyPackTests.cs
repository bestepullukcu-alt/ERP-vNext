using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementGovernancePolicyPack;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU31 — default governance policy pack seeder tests. Tenant-scoped, idempotent, non-destructive: Apply
/// creates only missing policies, skips existing keys, reports (never overwrites) conflicts, and never calls Update.
/// </summary>
public sealed class DocumentGovernancePolicyPackTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static (DocumentGovernancePolicyPackSeeder seeder, FakeRetentionRepo r, FakeGDocPRepo g, FakeSignatureRepo s)
        Build(Guid tenantId)
    {
        var tenant = new FakeTenantContext(tenantId);
        var r = new FakeRetentionRepo(tenant);
        var g = new FakeGDocPRepo(tenant);
        var s = new FakeSignatureRepo(tenant);
        var seeder = new DocumentGovernancePolicyPackSeeder(r, g, s, tenant, new FakeCurrentUser());
        return (seeder, r, g, s);
    }

    // ── 1 ── preview reports every default as creatable and writes nothing
    [Fact]
    public async Task Preview_default_policy_pack_returns_expected_sections()
    {
        var (seeder, r, g, s) = Build(TenantA);
        var result = await seeder.PreviewDefaultPolicyPackAsync("corr");

        Assert.Equal("Preview", result.ApplicationStatus);
        Assert.Equal(0, result.CreatedCount);
        Assert.All(result.Items, i => Assert.Equal(PolicyPackItemStatus.Missing, i.Status));
        Assert.Contains(result.Items, i => i.Family == "Retention");
        Assert.Contains(result.Items, i => i.Family == "GDocPCorrection");
        Assert.Contains(result.Items, i => i.Family == "Signature");
        Assert.Empty(r.Store);
        Assert.Empty(g.Store);
        Assert.Empty(s.Store); // nothing written on preview
    }

    // ── 2/3/4 ── apply creates each family
    [Fact]
    public async Task Apply_default_policy_pack_creates_all_families()
    {
        var (seeder, r, g, s) = Build(TenantA);
        var result = await seeder.ApplyDefaultPolicyPackAsync("corr");

        Assert.Equal(20, result.CreatedRetentionPolicyIds.Count);
        Assert.Equal(10, result.CreatedGDocPPolicyIds.Count);
        Assert.Equal(12, result.CreatedSignaturePolicyIds.Count);
        Assert.Equal(42, result.CreatedCount);
        Assert.Equal("Applied", result.ApplicationStatus);
        Assert.All(r.Store.Values, p => Assert.Equal(RetentionPolicyStatus.Active, p.PolicyStatus));
        Assert.All(s.Store.Values, p => Assert.Equal(SignaturePolicyStatus.Active, p.PolicyStatus));
        Assert.All(g.Store.Values, p => Assert.Equal(GDocPCorrectionPolicyStatus.Active, p.PolicyStatus));
    }

    // ── 5 ── idempotent: a second apply creates nothing and skips everything
    [Fact]
    public async Task Apply_default_policy_pack_is_idempotent()
    {
        var (seeder, _, _, _) = Build(TenantA);
        await seeder.ApplyDefaultPolicyPackAsync("corr");
        var second = await seeder.ApplyDefaultPolicyPackAsync("corr");

        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(42, second.SkippedExistingCount);
        Assert.Equal(0, second.ConflictCount);
        Assert.Equal("Applied", second.ApplicationStatus);
    }

    // ── 6 ── an existing key is skipped, the rest created
    [Fact]
    public async Task Apply_default_policy_pack_skips_existing_policy_key()
    {
        var (seeder, r, _, _) = Build(TenantA);
        r.Seed(new DocumentRetentionPolicy
        {
            Id = Guid.NewGuid(), TenantId = TenantA, PolicyKey = "RETENTION_IDENTIFIER_LEDGER_PERMANENT",
            PolicyName = "pre-existing", PolicyStatus = RetentionPolicyStatus.Active,
            SubjectType = RetentionSubjectType.IdentifierAllocationLedger, IsPermanentRetention = true, RegulatoryBasis = "x"
        });

        var result = await seeder.ApplyDefaultPolicyPackAsync("corr");

        Assert.Equal(41, result.CreatedCount);
        Assert.Equal(1, result.SkippedExistingCount);
        Assert.DoesNotContain(result.Items, i => i.PolicyKey == "RETENTION_IDENTIFIER_LEDGER_PERMANENT" && i.Status == PolicyPackItemStatus.Created);
    }

    // ── 7/21 ── an existing key with divergent core fields is a conflict, NOT overwritten
    [Fact]
    public async Task Apply_default_policy_pack_reports_conflict_without_overwrite()
    {
        var (seeder, r, _, _) = Build(TenantA);
        var pre = new DocumentRetentionPolicy
        {
            Id = Guid.NewGuid(), TenantId = TenantA, PolicyKey = "RETENTION_CONTROLLED_COPY_10Y",
            PolicyName = "diverged", PolicyStatus = RetentionPolicyStatus.Active,
            SubjectType = RetentionSubjectType.ApprovalEvidence /* different subject */, MinimumRetentionYears = 3, RegulatoryBasis = "x"
        };
        r.Seed(pre);

        var result = await seeder.ApplyDefaultPolicyPackAsync("corr");

        Assert.True(result.ConflictCount >= 1);
        Assert.Contains(result.WarningMessages, w => w.Contains("RETENTION_CONTROLLED_COPY_10Y"));
        Assert.Equal("AppliedWithWarnings", result.ApplicationStatus);
        // not overwritten: the pre-existing row is unchanged
        var stored = r.Store["RETENTION_CONTROLLED_COPY_10Y"];
        Assert.Equal(pre.Id, stored.Id);
        Assert.Equal(RetentionSubjectType.ApprovalEvidence, stored.SubjectType);
        Assert.Equal(3, stored.MinimumRetentionYears);
    }

    // ── 8 ── controlled document retains while effective + at least 10 years
    [Fact]
    public async Task Retention_policy_controlled_document_retains_while_effective_plus_10y()
    {
        var (seeder, r, _, _) = Build(TenantA);
        await seeder.ApplyDefaultPolicyPackAsync("corr");

        var p = r.Store["RETENTION_CONTROLLED_DOCUMENT_10Y_AFTER_RETIREMENT_OR_SUPERSESSION"];
        Assert.Equal(RetentionSubjectType.ControlledDocument, p.SubjectType);
        Assert.True(p.RetainWhileEffective);
        Assert.True(p.EffectiveRetentionYears() >= 10);
    }

    // ── 9 ── identifier ledger is permanent
    [Fact]
    public async Task Retention_policy_identifier_ledger_is_permanent()
    {
        var (seeder, r, _, _) = Build(TenantA);
        await seeder.ApplyDefaultPolicyPackAsync("corr");
        Assert.True(r.Store["RETENTION_IDENTIFIER_LEDGER_PERMANENT"].IsPermanentRetention);
    }

    // ── 10 ── signature record retention 10y created
    [Fact]
    public async Task Retention_policy_signature_record_10y_created()
    {
        var (seeder, r, _, _) = Build(TenantA);
        await seeder.ApplyDefaultPolicyPackAsync("corr");
        var p = r.Store["RETENTION_SIGNATURE_RECORD_10Y"];
        Assert.Equal(RetentionSubjectType.DocumentSignatureRecord, p.SubjectType);
        Assert.True(p.EffectiveRetentionYears() >= 10);
    }

    // ── 11 ── timestamp correction requires evidence + review + backdating sensitive
    [Fact]
    public async Task GDocP_timestamp_policy_requires_evidence_review_and_backdating_sensitive()
    {
        var (seeder, _, g, _) = Build(TenantA);
        await seeder.ApplyDefaultPolicyPackAsync("corr");
        var p = g.Store["GDOCP_REGULATED_TIMESTAMP_CORRECTION"];
        Assert.True(p.RequiresCorrectionReason);
        Assert.True(p.RequiresEvidenceReference);
        Assert.True(p.RequiresReview);
        Assert.True(p.IsBackdatingSensitive);
    }

    // ── 12 ── reconstruction requires a deviation reference for high risk
    [Fact]
    public async Task GDocP_reconstruction_policy_requires_deviation_for_high_risk()
    {
        var (seeder, _, g, _) = Build(TenantA);
        await seeder.ApplyDefaultPolicyPackAsync("corr");
        Assert.True(g.Store["GDOCP_RECONSTRUCTION_CORRECTION"].RequiresDeviationReferenceForHighRisk);
    }

    // ── 13 ── signature requires meaning + fingerprint + manifestation
    [Fact]
    public async Task Signature_policy_requires_meaning_fingerprint_manifestation()
    {
        var (seeder, _, _, s) = Build(TenantA);
        await seeder.ApplyDefaultPolicyPackAsync("corr");
        Assert.All(s.Store.Values, p =>
        {
            Assert.True(p.RequiresMeaningStatement);
            Assert.True(p.RequiresObjectFingerprint);
            Assert.True(p.RequiresManifestation);
        });
    }

    // ── 14 ── unapproved repository is never in a default allow-list
    [Fact]
    public async Task Signature_policy_disallows_unapproved_repository()
    {
        var (seeder, _, _, s) = Build(TenantA);
        await seeder.ApplyDefaultPolicyPackAsync("corr");
        Assert.All(s.Store.Values, p => Assert.DoesNotContain(RepositoryType.UnapprovedRepository, p.AllowedRepositoryTypes));
    }

    // ── 15 ── second factor never required by default (no platform 2FA context)
    [Fact]
    public async Task Signature_policy_does_not_require_second_factor_by_default()
    {
        var (seeder, _, _, s) = Build(TenantA);
        await seeder.ApplyDefaultPolicyPackAsync("corr");
        Assert.All(s.Store.Values, p => Assert.False(p.RequiresSecondFactor));
    }

    // ── 19 ── the application result carries the pack history summary
    [Fact]
    public async Task Policy_pack_application_result_records_summary()
    {
        var (seeder, _, _, _) = Build(TenantA);
        var result = await seeder.ApplyDefaultPolicyPackAsync("corr");
        Assert.Equal(DocumentGovernancePolicyPackManifest.PackKey, result.PackKey);
        Assert.Equal(DocumentGovernancePolicyPackManifest.PackVersion, result.PackVersion);
        Assert.Equal(TenantA, result.TenantId);
        Assert.Equal(42, result.Items.Count);
    }

    // ── 20 ── tenant-scoped: created policies carry the resolved tenant, another tenant sees nothing
    [Fact]
    public async Task Apply_is_tenant_scoped_and_cross_tenant_isolated()
    {
        var (seederA, rA, _, _) = Build(TenantA);
        await seederA.ApplyDefaultPolicyPackAsync("corr");
        Assert.All(rA.Store.Values, p => Assert.Equal(TenantA, p.TenantId));

        // A fresh tenant-B context sees none of tenant A's policies.
        var (_, rB, _, _) = Build(TenantB);
        Assert.Empty(rB.Store);
    }

    [Fact]
    public async Task Apply_throws_when_tenant_unresolved()
    {
        var tenant = new FakeTenantContext(null);
        var seeder = new DocumentGovernancePolicyPackSeeder(
            new FakeRetentionRepo(tenant), new FakeGDocPRepo(tenant), new FakeSignatureRepo(tenant), tenant, new FakeCurrentUser());

        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.ApplyDefaultPolicyPackAsync("corr"));
    }

    // ── 22 ── apply never mutates an existing record (no UpdateAsync call)
    [Fact]
    public async Task Apply_never_calls_update()
    {
        var (seeder, r, g, s) = Build(TenantA);
        await seeder.ApplyDefaultPolicyPackAsync("corr");
        await seeder.ApplyDefaultPolicyPackAsync("corr"); // second run hits the skip path
        Assert.Equal(0, r.UpdateCalls);
        Assert.Equal(0, g.UpdateCalls);
        Assert.Equal(0, s.UpdateCalls);
    }

    // ── 23 ── the FU15 retention subject vocabulary ordinals must not shift (persisted values)
    [Fact]
    public void RetentionSubjectType_ordinals_are_stable()
    {
        Assert.Equal(27, (int)RetentionSubjectType.Other);
        Assert.Equal(0, (int)RetentionSubjectType.ControlledDocument);
        Assert.Equal(3, (int)RetentionSubjectType.IdentifierAllocationLedger);
        Assert.Equal(43, (int)RetentionSubjectType.DocumentSignatureRecord);
    }

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
        public Guid UserId => Guid.Parse("cccccccc-0000-0000-0000-000000000003");
        public string? Email => "seed@diten.test";
        public string? DisplayName => "Seed";
        public string ActorName => "seed-runner";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeRetentionRepo(ITenantContext tenant) : IDocumentRetentionPolicyRepository
    {
        public readonly Dictionary<string, DocumentRetentionPolicy> Store = new(StringComparer.OrdinalIgnoreCase);
        public int UpdateCalls;
        public void Seed(DocumentRetentionPolicy p) => Store[p.PolicyKey] = p;

        public Task<DocumentRetentionPolicy> CreateAsync(DocumentRetentionPolicy policy, CancellationToken ct = default)
        {
            Assert.Equal(tenant.TenantId, policy.TenantId); // tenant scoping honored
            Store[policy.PolicyKey] = policy;
            return Task.FromResult(policy);
        }
        public Task<DocumentRetentionPolicy?> GetByKeyAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(Store.TryGetValue(key, out var p) ? p : null);
        public Task<DocumentRetentionPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Store.Values.FirstOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<DocumentRetentionPolicy>> GetActiveBySubjectTypeAsync(RetentionSubjectType t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRetentionPolicy>>(Store.Values.Where(p => p.SubjectType == t).ToList());
        public Task<IReadOnlyList<DocumentRetentionPolicy>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRetentionPolicy>>(Store.Values.ToList());
        public Task<bool> UpdateAsync(DocumentRetentionPolicy policy, CancellationToken ct = default) { UpdateCalls++; Store[policy.PolicyKey] = policy; return Task.FromResult(true); }
    }

    private sealed class FakeGDocPRepo(ITenantContext tenant) : IDocumentGDocPCorrectionPolicyRepository
    {
        public readonly Dictionary<string, DocumentGDocPCorrectionPolicy> Store = new(StringComparer.OrdinalIgnoreCase);
        public int UpdateCalls;
        public void Seed(DocumentGDocPCorrectionPolicy p) => Store[p.PolicyKey] = p;

        public Task<DocumentGDocPCorrectionPolicy> CreateAsync(DocumentGDocPCorrectionPolicy policy, CancellationToken ct = default)
        {
            Assert.Equal(tenant.TenantId, policy.TenantId);
            Store[policy.PolicyKey] = policy;
            return Task.FromResult(policy);
        }
        public Task<DocumentGDocPCorrectionPolicy?> GetByKeyAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(Store.TryGetValue(key, out var p) ? p : null);
        public Task<DocumentGDocPCorrectionPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Store.Values.FirstOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<DocumentGDocPCorrectionPolicy>> GetActiveBySubjectTypeAsync(GDocPSubjectType t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionPolicy>>(Store.Values.Where(p => p.SubjectType == t).ToList());
        public Task<IReadOnlyList<DocumentGDocPCorrectionPolicy>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionPolicy>>(Store.Values.ToList());
        public Task<bool> UpdateAsync(DocumentGDocPCorrectionPolicy policy, CancellationToken ct = default) { UpdateCalls++; Store[policy.PolicyKey] = policy; return Task.FromResult(true); }
    }

    private sealed class FakeSignatureRepo(ITenantContext tenant) : IDocumentSignaturePolicyRepository
    {
        public readonly Dictionary<string, DocumentSignaturePolicy> Store = new(StringComparer.OrdinalIgnoreCase);
        public int UpdateCalls;
        public void Seed(DocumentSignaturePolicy p) => Store[p.PolicyKey] = p;

        public Task<DocumentSignaturePolicy> CreateAsync(DocumentSignaturePolicy policy, CancellationToken ct = default)
        {
            Assert.Equal(tenant.TenantId, policy.TenantId);
            Store[policy.PolicyKey] = policy;
            return Task.FromResult(policy);
        }
        public Task<DocumentSignaturePolicy?> GetByKeyAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(Store.TryGetValue(key, out var p) ? p : null);
        public Task<DocumentSignaturePolicy?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Store.Values.FirstOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<DocumentSignaturePolicy>> GetActiveBySubjectTypeAsync(SignableSubjectType t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignaturePolicy>>(Store.Values.Where(p => p.SignableSubjectType == t).ToList());
        public Task<IReadOnlyList<DocumentSignaturePolicy>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignaturePolicy>>(Store.Values.ToList());
        public Task<bool> UpdateAsync(DocumentSignaturePolicy policy, CancellationToken ct = default) { UpdateCalls++; Store[policy.PolicyKey] = policy; return Task.FromResult(true); }
    }
}
