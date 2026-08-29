using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementLifecycle;
using Diten.Platform.Application.Features.DocumentManagementLifecycle.Services;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU10 — non-waivable release gate engine tests. Tenant-aware in-memory fakes exercise the six gates, manual
/// evidence, computed gates (FU07 UID/code → gate 1, FU09 approval → gate 3), immutability, and the FU08 MarkEffective
/// hard-gate seam.
/// </summary>
public sealed class DocumentReleaseGateTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private const string Corr = "fu10-corr-1";

    [Fact]
    public async Task Gate1_passes_when_register_active_uid_code_present()
    {
        var f = Fixture();
        var e = SeedEntry(f, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");

        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal("Yes", Gate(r, 1).GateResult);
    }

    [Fact]
    public async Task Gate1_blocks_when_uid_missing()
    {
        var f = Fixture();
        var e = SeedEntry(f, uid: null, code: "GMG-QMS-SOP-0001");

        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal("No", Gate(r, 1).GateResult);
        Assert.Equal(ReleaseGateEvaluationStatus.Blocked.ToString(), r.Data!.EvaluationStatus);
    }

    [Fact]
    public async Task Gate1_blocks_when_code_missing()
    {
        var f = Fixture();
        var e = SeedEntry(f, uid: "UID-0000001", code: null);

        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal("No", Gate(r, 1).GateResult);
    }

    [Fact]
    public async Task Gate2_blocks_when_repository_evidence_missing()
    {
        var f = Fixture();
        var e = SeedEntry(f);
        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);
        Assert.Equal("No", Gate(r, 2).GateResult);
    }

    [Fact]
    public async Task Gate2_passes_with_valid_repository_evidence()
    {
        var f = Fixture();
        var e = SeedEntry(f);
        await f.Evaluator.RecordEvidenceAsync(e.Id, Evidence("ApprovedRepositoryAvailable", "REPO-ASSESS-1"), Corr, CancellationToken.None);

        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);
        Assert.Equal("Yes", Gate(r, 2).GateResult);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("SegregationFailed")]
    [InlineData("Rejected")]
    public async Task Gate3_blocks_when_approval_not_complete(string approvalStatus)
    {
        var f = Fixture();
        var e = SeedEntry(f);
        e.ApprovalEvidenceStatus = approvalStatus;

        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);
        Assert.Equal("No", Gate(r, 3).GateResult);
    }

    [Fact]
    public async Task Gate3_passes_when_approval_evidence_complete()
    {
        var f = Fixture();
        var e = SeedEntry(f);
        e.ApprovalEvidenceStatus = "Complete";

        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);
        Assert.Equal("Yes", Gate(r, 3).GateResult);
    }

    [Fact]
    public async Task Gate4_blocks_without_required_materials_evidence()
    {
        var f = Fixture();
        var e = SeedEntry(f);
        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);
        Assert.Equal("No", Gate(r, 4).GateResult);
    }

    [Fact]
    public async Task Gate4_passes_with_manual_evidence()
    {
        var f = Fixture();
        var e = SeedEntry(f);
        await f.Evaluator.RecordEvidenceAsync(e.Id, Evidence("RequiredExecutionMaterialsEffective", "FORMS-EFFECTIVE-1"), Corr, CancellationToken.None);
        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);
        Assert.Equal("Yes", Gate(r, 4).GateResult);
    }

    [Fact]
    public async Task Gate5_blocks_for_critical_without_training_evidence()
    {
        var f = Fixture();
        var e = SeedEntry(f, criticality: DocumentCriticality.Critical);
        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);
        Assert.Equal("No", Gate(r, 5).GateResult);
    }

    [Fact]
    public async Task Gate5_passes_for_critical_with_training_evidence()
    {
        var f = Fixture();
        var e = SeedEntry(f, criticality: DocumentCriticality.Critical);
        await f.Evaluator.RecordEvidenceAsync(e.Id, Evidence("TrainingReadiness", "TRAIN-MTX-1"), Corr, CancellationToken.None);
        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);
        Assert.Equal("Yes", Gate(r, 5).GateResult);
    }

    [Fact]
    public async Task Gate6_blocks_without_withdrawal_method_evidence()
    {
        var f = Fixture();
        var e = SeedEntry(f);
        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);
        Assert.Equal("No", Gate(r, 6).GateResult);
    }

    [Fact]
    public async Task Gate6_passes_with_withdrawal_evidence()
    {
        var f = Fixture();
        var e = SeedEntry(f);
        await f.Evaluator.RecordEvidenceAsync(e.Id, Evidence("SupersededCopyWithdrawalMethod", "WITHDRAW-WI-1"), Corr, CancellationToken.None);
        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);
        Assert.Equal("Yes", Gate(r, 6).GateResult);
    }

    [Fact]
    public async Task Recording_evidence_without_reference_is_rejected()
    {
        var f = Fixture();
        var e = SeedEntry(f);

        var r = await f.Evaluator.RecordEvidenceAsync(e.Id, Evidence("ApprovedRepositoryAvailable", ""), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(ReleaseGateReasonCodes.EvidenceIncomplete, r.ReasonCode);
    }

    [Fact]
    public async Task Recorded_evidence_always_has_verifier_and_date()
    {
        var f = Fixture();
        var e = SeedEntry(f);
        await f.Evaluator.RecordEvidenceAsync(e.Id, Evidence("RequiredExecutionMaterialsEffective", "REF-1"), Corr, CancellationToken.None);

        var ev = f.Evidence.Items.Single();
        Assert.NotEqual(Guid.Empty, ev.VerifiedByUserId);
        Assert.NotEqual(default, ev.VerificationDate);
    }

    [Fact]
    public async Task ExceptionPermitted_is_always_false()
    {
        var f = Fixture();
        var e = SeedEntry(f);
        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);
        Assert.All(r.Data!.Gates, g => Assert.False(g.ExceptionPermitted));
        Assert.All(r.Data.Gates, g => Assert.True(g.IsNonWaivable));
    }

    [Fact]
    public async Task Evaluation_complete_when_all_6_gates_pass()
    {
        var f = Fixture();
        var e = FullyReadyEntry(f);

        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal(ReleaseGateEvaluationStatus.Complete.ToString(), r.Data!.EvaluationStatus);
        Assert.True(r.Data.Ready);
        Assert.Equal(6, r.Data.CompletedGateCount);
    }

    [Fact]
    public async Task Evaluation_blocked_when_any_gate_fails()
    {
        var f = Fixture();
        var e = FullyReadyEntry(f);
        e.ApprovalEvidenceStatus = "Pending"; // break gate 3

        var r = await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal(ReleaseGateEvaluationStatus.Blocked.ToString(), r.Data!.EvaluationStatus);
        Assert.False(r.Data.Ready);
    }

    [Fact]
    public async Task Evaluation_updates_register_entry_extension_fields()
    {
        var f = Fixture();
        var e = SeedEntry(f);

        await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        var after = f.Register.Items.Single(x => x.Id == e.Id);
        Assert.Equal(ReleaseGateEvaluationStatus.Blocked.ToString(), after.LastReleaseGateEvaluationStatus);
        Assert.NotNull(after.LastReleaseGateEvaluationAt);
        Assert.NotNull(after.LastReleaseGateBlockingCount);
    }

    [Fact]
    public async Task Evidence_history_is_appended_and_never_hard_deleted()
    {
        var f = Fixture();
        var e = SeedEntry(f);
        await f.Evaluator.RecordEvidenceAsync(e.Id, Evidence("SupersededCopyWithdrawalMethod", "W-1"), Corr, CancellationToken.None);
        await f.Evaluator.RecordEvidenceAsync(e.Id, Evidence("SupersededCopyWithdrawalMethod", "W-2"), Corr, CancellationToken.None);

        Assert.Equal(2, f.Evidence.Items.Count(x => x.RegisterEntryId == e.Id));
        Assert.DoesNotContain(f.Evidence.Items, x => x.IsDeleted);
    }

    [Fact]
    public async Task Re_evaluation_creates_new_evaluation_without_deleting_history()
    {
        var f = Fixture();
        var e = SeedEntry(f);
        await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);
        await f.Evaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal(2, f.Evaluations.Items.Count(x => x.RegisterEntryId == e.Id));
        Assert.DoesNotContain(f.Evaluations.Items, x => x.IsDeleted);
    }

    [Fact]
    public async Task Cross_tenant_evidence_is_blocked()
    {
        var f = Fixture();
        var foreign = SeedEntry(f, tenantId: OtherTenantId);

        var r = await f.Evaluator.RecordEvidenceAsync(foreign.Id, Evidence("RequiredExecutionMaterialsEffective", "REF"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task MarkEffective_blocks_when_release_gate_incomplete_and_required()
    {
        var f = Fixture();
        var e = SeedEntry(f, uid: "UID-0000001", code: "GMG-QMS-SOP-0001", criticality: DocumentCriticality.Critical);
        e.ApprovalEvidenceStatus = "Complete";
        e.LifecycleStatus = ControlledDocumentLifecycleStatus.ApprovedPendingEffective;
        var lifecycle = LifecycleService(f);

        var r = await lifecycle.TransitionAsync(e.Id, Transition("Effective"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.ReleaseGateIncomplete, r.ReasonCode);
    }

    [Fact]
    public async Task MarkEffective_allows_when_release_gate_complete()
    {
        var f = Fixture();
        var e = FullyReadyEntry(f, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");
        e.LifecycleStatus = ControlledDocumentLifecycleStatus.ApprovedPendingEffective;
        var lifecycle = LifecycleService(f);

        var r = await lifecycle.TransitionAsync(e.Id, Transition("Effective"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Effective", r.Data!.CurrentStatus);
    }

    [Fact]
    public async Task MarkEffective_legacy_entry_without_gate_requirement_is_compatible()
    {
        var f = Fixture();
        // Non-critical, not flagged, policy off → not subject to hard gating even with the port present.
        var e = SeedEntry(f, uid: "UID-0000001", code: "GMG-QMS-SOP-0001", criticality: DocumentCriticality.Minor);
        e.LifecycleStatus = ControlledDocumentLifecycleStatus.ApprovedPendingEffective;
        var lifecycle = LifecycleService(f);

        var r = await lifecycle.TransitionAsync(e.Id, Transition("Effective"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static ReleaseGateResultModel Gate(Response<ReleaseGateEvaluationModel> r, int gateNumber) =>
        r.Data!.Gates.Single(g => g.GateNumber == gateNumber);

    private static RecordReleaseGateEvidenceInput Evidence(string gateKey, string reference) =>
        new(gateKey, reference, null, "QADocumentation", null, null);

    private static TransitionDocumentLifecycleInput Transition(string status) =>
        new(status, null, "REL-EVIDENCE", null, null, null, null);

    private static DocumentLifecycleService LifecycleService(Harness f)
    {
        var adapter = new ReleaseGateEvaluationPortAdapter(f.Evaluator, Options.Create(new DocumentLifecycleOptions()));
        return new DocumentLifecycleService(f.Register, new FakeTransitionRepo(f.Tenant), f.Tenant, new FakeUser(),
            Options.Create(new DocumentLifecycleOptions()), approvalGate: null, releaseGate: adapter);
    }

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var register = new FakeRegisterRepo(tenant);
        var evaluations = new FakeEvaluationRepo(tenant);
        var results = new FakeResultRepo(tenant);
        var evidence = new FakeEvidenceRepo(tenant);
        var evaluator = new DocumentReleaseGateEvaluator(register, evaluations, results, evidence, tenant, new FakeUser(),
            Options.Create(new DocumentReleaseGateOptions()));
        return new Harness(evaluator, register, evaluations, results, evidence, tenant);
    }

    private static DocumentMasterRegisterEntry SeedEntry(
        Harness f, string? uid = "UID-0000001", string? code = "GMG-QMS-SOP-0001",
        DocumentCriticality criticality = DocumentCriticality.Major, Guid? tenantId = null)
    {
        var e = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? TenantId,
            DocumentTitle = "Document Control",
            DocumentClass = ControlledDocumentClass.Sop,
            DocumentType = DocumentType.Sop,
            Criticality = criticality,
            IsControlledDocument = true,
            PermanentUid = uid,
            DocumentCode = code,
            RegisterStatus = DocumentRegisterStatus.Active,
            LifecycleStatus = ControlledDocumentLifecycleStatus.Draft
        };
        f.Register.Items.Add(e);
        return e;
    }

    private static DocumentMasterRegisterEntry FullyReadyEntry(Harness f, string? uid = "UID-0000001", string? code = "GMG-QMS-SOP-0001")
    {
        var e = SeedEntry(f, uid, code, DocumentCriticality.Critical);
        e.ApprovalEvidenceStatus = "Complete";
        foreach (var key in new[] { ReleaseGateKey.ApprovedRepositoryAvailable, ReleaseGateKey.RequiredExecutionMaterialsEffective, ReleaseGateKey.TrainingReadiness, ReleaseGateKey.SupersededCopyWithdrawalMethod })
        {
            f.Evidence.Items.Add(new DocumentReleaseGateEvidence
            {
                Id = Guid.NewGuid(),
                TenantId = e.TenantId,
                RegisterEntryId = e.Id,
                GateKey = key,
                EvidenceReference = $"EV-{key}",
                VerifiedByUserId = Guid.Parse("dddddddd-1111-2222-3333-444444444444"),
                VerifiedByRole = "QADocumentation",
                VerificationDate = DateTimeOffset.UtcNow
            });
        }
        return e;
    }

    private sealed record Harness(
        DocumentReleaseGateEvaluator Evaluator, FakeRegisterRepo Register, FakeEvaluationRepo Evaluations,
        FakeResultRepo Results, FakeEvidenceRepo Evidence, ITenantContext Tenant);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444444");
        public string? Email => "fu10@example.test";
        public string? DisplayName => "FU10 Tester";
        public string ActorName => "fu10@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeRegisterRepo(ITenantContext tenant) : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];
        private IEnumerable<DocumentMasterRegisterEntry> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) { Items.Add(entry); return Task.FromResult(entry); }
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string permanentUid, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.PermanentUid == permanentUid));
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string documentCode, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.DocumentCode == documentCode));
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.ControlledDocumentId == controlledDocumentId));
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default)
        {
            var q = Scoped;
            if (filter.LifecycleStatus is { } ls) q = q.Where(x => x.LifecycleStatus == ls);
            return Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(q.ToList());
        }
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == entry.Id); if (i >= 0) Items[i] = entry; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeEvaluationRepo(ITenantContext tenant) : IDocumentReleaseGateEvaluationRepository
    {
        public List<DocumentReleaseGateEvaluation> Items { get; } = [];
        private IEnumerable<DocumentReleaseGateEvaluation> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentReleaseGateEvaluation> CreateAsync(DocumentReleaseGateEvaluation evaluation, CancellationToken ct = default) { Items.Add(evaluation); return Task.FromResult(evaluation); }
        public Task<DocumentReleaseGateEvaluation?> GetLatestAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult(Scoped.Where(x => x.RegisterEntryId == registerEntryId).OrderByDescending(x => x.EvaluatedAt).FirstOrDefault());
        public Task<IReadOnlyList<DocumentReleaseGateEvaluation>> GetHistoryAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentReleaseGateEvaluation>>(Scoped.Where(x => x.RegisterEntryId == registerEntryId).OrderByDescending(x => x.EvaluatedAt).ToList());
    }

    private sealed class FakeResultRepo(ITenantContext tenant) : IDocumentReleaseGateResultRepository
    {
        public List<DocumentReleaseGateResult> Items { get; } = [];
        public Task<DocumentReleaseGateResult> CreateAsync(DocumentReleaseGateResult result, CancellationToken ct = default) { Items.Add(result); return Task.FromResult(result); }
        public Task<IReadOnlyList<DocumentReleaseGateResult>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentReleaseGateResult>>(Items.Where(x => x.TenantId == tenant.TenantId && x.EvaluationId == evaluationId).OrderBy(x => x.GateNumber).ToList());
    }

    private sealed class FakeEvidenceRepo(ITenantContext tenant) : IDocumentReleaseGateEvidenceRepository
    {
        public List<DocumentReleaseGateEvidence> Items { get; } = [];
        private IEnumerable<DocumentReleaseGateEvidence> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentReleaseGateEvidence> CreateAsync(DocumentReleaseGateEvidence evidence, CancellationToken ct = default) { Items.Add(evidence); return Task.FromResult(evidence); }
        public Task<IReadOnlyList<DocumentReleaseGateEvidence>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentReleaseGateEvidence>>(Scoped.Where(x => x.RegisterEntryId == registerEntryId).ToList());
        public Task<DocumentReleaseGateEvidence?> GetLatestForGateAsync(Guid registerEntryId, ReleaseGateKey gateKey, CancellationToken ct = default) =>
            Task.FromResult(Scoped.Where(x => x.RegisterEntryId == registerEntryId && x.GateKey == gateKey).OrderByDescending(x => x.VerificationDate).FirstOrDefault());
    }

    private sealed class FakeTransitionRepo(ITenantContext tenant) : IDocumentLifecycleTransitionRecordRepository
    {
        public List<DocumentLifecycleTransitionRecord> Items { get; } = [];
        public Task<DocumentLifecycleTransitionRecord> CreateAsync(DocumentLifecycleTransitionRecord record, CancellationToken ct = default) { Items.Add(record); return Task.FromResult(record); }
        public Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLifecycleTransitionRecord>>(Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == registerEntryId).ToList());
        public Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLifecycleTransitionRecord>>(Items.Where(x => x.TenantId == tenant.TenantId).ToList());
    }
}
