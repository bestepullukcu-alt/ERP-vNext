using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates.Services;
using Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment;
using Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU16 — repository assessment / DMS boundary tests. Tenant-aware in-memory fakes exercise assessment
/// content findings, the type-specific boundary, approval, register linking and FU10 Gate 2 computed behaviour.
/// </summary>
public sealed class DocumentRepositoryAssessmentTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid Owner = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    private const string Corr = "fu16-corr-1";

    // ── content / findings / boundary ─────────────────────────────────────────

    [Fact]
    public async Task Create_repository_assessment_draft()
    {
        var f = Fixture();
        var r = await f.Service.CreateAsync(Interim("Team Drive"), Corr, CancellationToken.None);
        Assert.True(r.IsSuccessful);
        Assert.Equal("Draft", r.Data!.AssessmentStatus);
        Assert.Equal("ApprovedInterimRepository", r.Data.RepositoryType);
    }

    [Fact]
    public async Task Evaluate_interim_repository_missing_owner_creates_finding()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(Interim("Drive") with { RepositoryOwnerUserId = null, RepositoryOwnerRole = null }, Corr, CancellationToken.None);

        var r = await f.Service.EvaluateAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.Contains(r.Data!.BlockingFindings, x => x.FindingType == nameof(RepositoryFindingType.MissingOwner));
        Assert.Contains(f.Findings.Items, x => x.FindingType == RepositoryFindingType.MissingOwner);
    }

    [Fact]
    public async Task Evaluate_interim_repository_missing_exact_location_blocks_gate_support()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(Interim("Drive") with { ExactLocation = null }, Corr, CancellationToken.None);

        var r = await f.Service.EvaluateAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.False(r.Data!.CanSupportReleaseGate);
        Assert.Contains(r.Data.BlockingFindings, x => x.FindingType == nameof(RepositoryFindingType.MissingExactLocation));
    }

    [Fact]
    public async Task Evaluate_interim_repository_with_required_fields_can_support_release_gate()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(Interim("Drive"), Corr, CancellationToken.None);

        var r = await f.Service.EvaluateAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.True(r.Data!.CanSupportReleaseGate);
        Assert.False(r.Data.CanSupportRegulatedESignature); // interim cannot claim regulated e-signature
        Assert.Contains("interim", r.Data.BoundaryStatement.ToLowerInvariant());
    }

    [Fact]
    public async Task ValidatedDms_requires_audit_trail_change_control_and_validation_evidence()
    {
        var f = Fixture();
        // A validated DMS missing audit trail / change control / validation evidence.
        var created = await f.Service.CreateAsync(Dms("DMS") with { AuditTrailDescription = null, ChangeControlDescription = null, ValidationEvidenceReference = null }, Corr, CancellationToken.None);

        var r = await f.Service.EvaluateAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.False(r.Data!.CanSupportReleaseGate);
        Assert.Contains(r.Data.BlockingFindings, x => x.FindingType == nameof(RepositoryFindingType.MissingAuditTrail));
        Assert.Contains(r.Data.BlockingFindings, x => x.FindingType == nameof(RepositoryFindingType.MissingChangeControl));
    }

    [Fact]
    public async Task ValidatedDms_with_full_content_can_support_regulated_e_signature()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(Dms("DMS"), Corr, CancellationToken.None);

        var r = await f.Service.EvaluateAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.True(r.Data!.CanSupportRegulatedESignature);
        Assert.Contains("validated dms", r.Data.BoundaryStatement.ToLowerInvariant());
    }

    [Fact]
    public async Task SeparateApprovalMechanism_does_not_claim_validated_dms()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(Interim("Sep") with { RepositoryType = "SeparateApprovalMechanism" }, Corr, CancellationToken.None);

        var r = await f.Service.EvaluateAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.False(r.Data!.CanSupportRegulatedESignature);
        Assert.Contains("separate approval mechanism", r.Data.BoundaryStatement.ToLowerInvariant());
    }

    [Fact]
    public async Task UnapprovedRepository_cannot_support_release_gate()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(Interim("Junk") with { RepositoryType = "UnapprovedRepository" }, Corr, CancellationToken.None);

        var r = await f.Service.EvaluateAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.False(r.Data!.CanSupportReleaseGate);
    }

    [Fact]
    public async Task Interim_checkpoint_overdue_creates_finding()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(Interim("Drive") with { InterimCheckpointDueDate = DateTimeOffset.UtcNow.AddDays(-1) }, Corr, CancellationToken.None);

        var r = await f.Service.EvaluateAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.Contains(r.Data!.BlockingFindings, x => x.FindingType == nameof(RepositoryFindingType.InterimPeriodExpired));
    }

    // ── approval ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_repository_assessment_requires_required_fields()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(Interim("Drive") with { BackupMethodDescription = null }, Corr, CancellationToken.None);

        var r = await f.Service.ApproveAsync(created.Data!.Id, new ApproveRepositoryAssessmentInput("GQD", null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(RepositoryAssessmentReasonCodes.RequiredFieldsMissing, r.ReasonCode);
    }

    [Fact]
    public async Task Approve_repository_assessment_requires_permitted_role()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(Interim("Drive"), Corr, CancellationToken.None);

        var r = await f.Service.ApproveAsync(created.Data!.Id, new ApproveRepositoryAssessmentInput("DocumentOwner", null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(RepositoryAssessmentReasonCodes.ApproverRoleInvalid, r.ReasonCode);
    }

    // ── register link + Gate 2 ─────────────────────────────────────────────────

    [Fact]
    public async Task Link_repository_assessment_to_register_entry_populates_repository_fields()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Major);
        var a = await ApprovedAssessment(f);

        var r = await f.Service.LinkToRegisterAsync(e.Id, a.Id, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        var after = f.Register.Items.Single(x => x.Id == e.Id);
        Assert.Equal(a.Id.ToString(), after.ApprovedRepositoryId);
        Assert.Equal(a.RepositoryName, after.ApprovedRepositoryName);
        Assert.Equal(a.ExactLocation, after.ApprovedRepositoryPath);
    }

    [Fact]
    public async Task Gate2_passes_with_approved_repository_assessment()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical);
        var a = await ApprovedAssessment(f);
        await f.Service.LinkToRegisterAsync(e.Id, a.Id, Corr, CancellationToken.None);

        var r = await f.GateEvaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal("Yes", Gate(r, 2).GateResult);
    }

    [Fact]
    public async Task Gate2_blocks_when_no_assessment_for_critical_document()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical); // no assessment linked

        var r = await f.GateEvaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        var gate2 = Gate(r, 2);
        Assert.Equal("No", gate2.GateResult);
        Assert.Contains("REPOSITORY_ASSESSMENT_MISSING", gate2.BlockingReason);
    }

    [Fact]
    public async Task Gate2_legacy_manual_evidence_still_works_for_noncritical_without_assessment()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Minor); // non-critical, no assessment → fall back to manual
        // Seed manual Gate 2 evidence directly.
        f.GateEvidence.Items.Add(new DocumentReleaseGateEvidence
        {
            Id = Guid.NewGuid(), TenantId = TenantId, RegisterEntryId = e.Id, GateKey = ReleaseGateKey.ApprovedRepositoryAvailable,
            EvidenceReference = "MANUAL-REPO-1", VerifiedByUserId = Owner, VerifiedByRole = "ITCSVOwner", VerificationDate = DateTimeOffset.UtcNow
        });

        var r = await f.GateEvaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal("Yes", Gate(r, 2).GateResult);
    }

    [Fact]
    public async Task Gate2_blocks_when_linked_assessment_rejected()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical);
        var created = await f.Service.CreateAsync(Interim("Drive"), Corr, CancellationToken.None);
        await f.Service.LinkToRegisterAsync(e.Id, created.Data!.Id, Corr, CancellationToken.None);
        await f.Service.RejectAsync(created.Data.Id, new RejectRepositoryAssessmentInput("inadequate controls"), Corr, CancellationToken.None);

        var r = await f.GateEvaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal("No", Gate(r, 2).GateResult);
    }

    [Fact]
    public async Task Gate2_blocks_when_linked_assessment_under_review()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical);
        var created = await f.Service.CreateAsync(Interim("Drive"), Corr, CancellationToken.None);
        await f.Service.EvaluateAsync(created.Data!.Id, Corr, CancellationToken.None); // → UnderReview
        await f.Service.LinkToRegisterAsync(e.Id, created.Data.Id, Corr, CancellationToken.None);

        var r = await f.GateEvaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal("No", Gate(r, 2).GateResult); // linked but not Approved
    }

    // ── isolation / durability ────────────────────────────────────────────────

    [Fact]
    public async Task Cross_tenant_link_is_blocked()
    {
        var f = Fixture();
        var foreign = SeedEntry(f, DocumentCriticality.Major, tenantId: OtherTenantId);
        var a = await ApprovedAssessment(f);

        var r = await f.Service.LinkToRegisterAsync(foreign.Id, a.Id, Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Assessment_and_findings_are_never_hard_deleted()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(Interim("Drive") with { ExactLocation = null }, Corr, CancellationToken.None);
        await f.Service.EvaluateAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.DoesNotContain(f.Assessments.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Findings.Items, x => x.IsDeleted);
        Assert.NotEmpty(f.Assessments.Items);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static ReleaseGateResultModel Gate(Response<ReleaseGateEvaluationModel> r, int gateNumber) =>
        r.Data!.Gates.Single(g => g.GateNumber == gateNumber);

    private async Task<DocumentRepositoryAssessment> ApprovedAssessment(Harness f)
    {
        var created = await f.Service.CreateAsync(Interim("Drive"), Corr, CancellationToken.None);
        await f.Service.ApproveAsync(created.Data!.Id, new ApproveRepositoryAssessmentInput("GQD", DateTimeOffset.UtcNow.AddYears(1)), Corr, CancellationToken.None);
        return f.Assessments.Items.Single(x => x.Id == created.Data.Id);
    }

    private static RepositoryAssessmentFieldsInput Interim(string name) => new(
        name, "ApprovedInterimRepository", "GoogleDrive", Owner, "IT/CSV Owner", $"/{name}/qms",
        "QA publishes; users read", "Quarterly", "Nightly cloud backup", "Annual restore test",
        "Wet signature reconciled to record", "Locked read-only effective copies", AuditTrailDescription: null,
        ChangeControlDescription: null, ValidationEvidenceReference: null, MaxInterimPeriodDays: 180,
        InterimCheckpointDueDate: DateTimeOffset.UtcNow.AddDays(90), MigrationReconciliationRequired: false,
        MigrationReconciliationReference: null, AssessmentEvidenceReference: "ASSESS-1");

    private static RepositoryAssessmentFieldsInput Dms(string name) => Interim(name) with
    {
        RepositoryType = "ValidatedDms",
        AuditTrailDescription = "Non-disableable audit trail",
        ChangeControlDescription = "Config change control",
        ValidationEvidenceReference = "VAL-PKG-1",
        RestoreTestFrequency = "Annual"
    };

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var register = new FakeRegisterRepo(tenant);
        var assessments = new FakeAssessmentRepo(tenant);
        var findings = new FakeFindingRepo(tenant);
        var evaluator = new DocumentRepositoryAssessmentEvaluator();
        var service = new DocumentRepositoryAssessmentService(assessments, findings, register, evaluator, tenant, new FakeUser());
        var repositoryPort = new RepositoryReadinessPortAdapter(assessments, findings, evaluator);

        var gateEvidence = new FakeGateEvidenceRepo(tenant);
        var gateEvaluator = new DocumentReleaseGateEvaluator(register, new FakeGateEvalRepo(tenant), new FakeGateResultRepo(tenant), gateEvidence,
            tenant, new FakeUser(), Options.Create(new DocumentReleaseGateOptions()), trainingPort: null, repositoryPort: repositoryPort);

        return new Harness(service, gateEvaluator, register, assessments, findings, gateEvidence);
    }

    private static DocumentMasterRegisterEntry SeedEntry(Harness f, DocumentCriticality criticality, Guid? tenantId = null)
    {
        var e = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(), TenantId = tenantId ?? TenantId, DocumentTitle = "Document Control",
            DocumentClass = ControlledDocumentClass.Sop, DocumentType = DocumentType.Sop, Criticality = criticality,
            IsControlledDocument = true, RegisterStatus = DocumentRegisterStatus.Active,
            PermanentUid = "UID-0000001", DocumentCode = "GMG-QMS-SOP-0001"
        };
        f.Register.Items.Add(e);
        return e;
    }

    private sealed record Harness(
        DocumentRepositoryAssessmentService Service, DocumentReleaseGateEvaluator GateEvaluator,
        FakeRegisterRepo Register, FakeAssessmentRepo Assessments, FakeFindingRepo Findings, FakeGateEvidenceRepo GateEvidence);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444444");
        public string? Email => "fu16@example.test";
        public string? DisplayName => "FU16 Tester";
        public string ActorName => "fu16@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeRegisterRepo(ITenantContext tenant) : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];
        private IEnumerable<DocumentMasterRegisterEntry> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string uid, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.PermanentUid == uid));
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.DocumentCode == code));
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.ControlledDocumentId == id));
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry e, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == e.Id); if (i >= 0) Items[i] = e; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeAssessmentRepo(ITenantContext tenant) : IDocumentRepositoryAssessmentRepository
    {
        public List<DocumentRepositoryAssessment> Items { get; } = [];
        private IEnumerable<DocumentRepositoryAssessment> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentRepositoryAssessment> CreateAsync(DocumentRepositoryAssessment a, CancellationToken ct = default) { Items.Add(a); return Task.FromResult(a); }
        public Task<DocumentRepositoryAssessment?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentRepositoryAssessment>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentRepositoryAssessment>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentRepositoryAssessment a, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == a.Id); if (i >= 0) Items[i] = a; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeFindingRepo(ITenantContext tenant) : IDocumentRepositoryAssessmentFindingRepository
    {
        public List<DocumentRepositoryAssessmentFinding> Items { get; } = [];
        private IEnumerable<DocumentRepositoryAssessmentFinding> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentRepositoryAssessmentFinding> CreateAsync(DocumentRepositoryAssessmentFinding f, CancellationToken ct = default) { Items.Add(f); return Task.FromResult(f); }
        public Task<IReadOnlyList<DocumentRepositoryAssessmentFinding>> GetByAssessmentAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRepositoryAssessmentFinding>>(Scoped.Where(x => x.RepositoryAssessmentId == id).ToList());
        public Task<bool> UpdateAsync(DocumentRepositoryAssessmentFinding f, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == f.Id); if (i >= 0) Items[i] = f; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeGateEvalRepo(ITenantContext tenant) : IDocumentReleaseGateEvaluationRepository
    {
        public List<DocumentReleaseGateEvaluation> Items { get; } = [];
        public Task<DocumentReleaseGateEvaluation> CreateAsync(DocumentReleaseGateEvaluation e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentReleaseGateEvaluation?> GetLatestAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == id).OrderByDescending(x => x.EvaluatedAt).FirstOrDefault());
        public Task<IReadOnlyList<DocumentReleaseGateEvaluation>> GetHistoryAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentReleaseGateEvaluation>>(Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == id).ToList());
    }

    private sealed class FakeGateResultRepo(ITenantContext tenant) : IDocumentReleaseGateResultRepository
    {
        public List<DocumentReleaseGateResult> Items { get; } = [];
        public Task<DocumentReleaseGateResult> CreateAsync(DocumentReleaseGateResult r, CancellationToken ct = default) { Items.Add(r); return Task.FromResult(r); }
        public Task<IReadOnlyList<DocumentReleaseGateResult>> GetByEvaluationAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentReleaseGateResult>>(Items.Where(x => x.TenantId == tenant.TenantId && x.EvaluationId == id).OrderBy(x => x.GateNumber).ToList());
    }

    private sealed class FakeGateEvidenceRepo(ITenantContext tenant) : IDocumentReleaseGateEvidenceRepository
    {
        public List<DocumentReleaseGateEvidence> Items { get; } = [];
        public Task<DocumentReleaseGateEvidence> CreateAsync(DocumentReleaseGateEvidence e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<IReadOnlyList<DocumentReleaseGateEvidence>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentReleaseGateEvidence>>(Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == id).ToList());
        public Task<DocumentReleaseGateEvidence?> GetLatestForGateAsync(Guid id, ReleaseGateKey key, CancellationToken ct = default) => Task.FromResult(Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == id && x.GateKey == key).OrderByDescending(x => x.VerificationDate).FirstOrDefault());
    }
}
