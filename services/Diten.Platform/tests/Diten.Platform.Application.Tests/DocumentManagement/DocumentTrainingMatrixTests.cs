using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates.Services;
using Diten.Platform.Application.Features.DocumentManagementTraining;
using Diten.Platform.Application.Features.DocumentManagementTraining.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU11 — training matrix + readiness + FU10 Gate 5 integration tests. Tenant-aware in-memory fakes exercise
/// matrix resolution, assignment/completion/effectiveness/restriction, readiness, and Gate 5 computed behaviour.
/// </summary>
public sealed class DocumentTrainingMatrixTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid Learner = Guid.Parse("a0000000-0000-0000-0000-0000000000aa");
    private const string Corr = "fu11-corr-1";

    [Fact]
    public async Task Resolve_training_matrix_for_critical_document_creates_required_roles()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical);

        await f.Training.ResolveMatrixAsync(e.Id, Corr, CancellationToken.None);
        var keys = KeysOf(f, e.Id);

        Assert.Contains("Role:GQD:FullSopCompetencyAssessment", keys);
        Assert.Contains("Role:QADocumentation:FullSopCompetencyAssessment", keys);
        Assert.Contains("Role:DocumentOwner:FullSopCompetencyAssessment", keys);
        Assert.All(f.Requirements.Items.Where(x => x.RegisterEntryId == e.Id), r => Assert.True(r.EffectivenessCheckRequired));
    }

    [Fact]
    public async Task Resolve_training_matrix_for_RA_impact_adds_GRA_scenario_assessment()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Major);
        e.HasRaImpact = true;
        await f.Training.ResolveMatrixAsync(e.Id, Corr, CancellationToken.None);
        Assert.Contains("Role:GRA:ScenarioAssessment", KeysOf(f, e.Id));
    }

    [Fact]
    public async Task Resolve_training_matrix_for_PV_impact_adds_QPPV_scenario_assessment()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Major);
        e.HasPvImpact = true;
        await f.Training.ResolveMatrixAsync(e.Id, Corr, CancellationToken.None);
        Assert.Contains("Role:QPPV:ScenarioAssessment", KeysOf(f, e.Id));
    }

    [Fact]
    public async Task Resolve_training_matrix_for_ITCSV_impact_adds_ITCSV_scenario_assessment()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Major);
        e.HasDmsCsvImpact = true;
        await f.Training.ResolveMatrixAsync(e.Id, Corr, CancellationToken.None);
        Assert.Contains("Role:ITCSVOwner:ScenarioAssessment", KeysOf(f, e.Id));
    }

    [Fact]
    public async Task Resolve_is_idempotent()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical);
        await f.Training.ResolveMatrixAsync(e.Id, Corr, CancellationToken.None);
        var first = f.Requirements.Items.Count(x => x.RegisterEntryId == e.Id);
        await f.Training.ResolveMatrixAsync(e.Id, Corr, CancellationToken.None);
        Assert.Equal(first, f.Requirements.Items.Count(x => x.RegisterEntryId == e.Id));
    }

    [Fact]
    public async Task Add_manual_requirement_creates_requirement()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Minor);
        var r = await AddReq(f, e.Id, criticalProcess: false, effectiveness: false, mandatory: true);
        Assert.True(r.IsSuccessful);
        Assert.Single(f.Requirements.Items.Where(x => x.RegisterEntryId == e.Id));
    }

    [Fact]
    public async Task Assign_training_creates_assignment()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Minor);
        var req = (await AddReq(f, e.Id, false, false, true)).Data!;
        var r = await f.Training.AssignAsync(e.Id, new AssignTrainingInput(req.Id, Learner, "DocumentOwner", null, null), Corr, CancellationToken.None);
        Assert.True(r.IsSuccessful);
        Assert.Single(f.Assignments.Items);
    }

    [Fact]
    public async Task Completion_requires_evidence_reference()
    {
        var f = Fixture();
        var (_, assignmentId) = await Assigned(f, false, false);
        var r = await f.Training.CompleteAsync(f.Entry.Id, assignmentId, new CompleteTrainingInput(""), Corr, CancellationToken.None);
        Assert.False(r.IsSuccessful);
        Assert.Equal(TrainingReasonCodes.EvidenceRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Complete_read_and_understand_marks_assignment_completed()
    {
        var f = Fixture();
        var (_, assignmentId) = await Assigned(f, false, false);
        var r = await f.Training.CompleteAsync(f.Entry.Id, assignmentId, new CompleteTrainingInput("ACK-1"), Corr, CancellationToken.None);
        Assert.True(r.IsSuccessful);
        Assert.Equal("Completed", r.Data!.Status);
    }

    [Fact]
    public async Task Critical_competency_requires_effectiveness_check()
    {
        var f = Fixture();
        var (_, assignmentId) = await Assigned(f, criticalProcess: true, effectiveness: true);
        var assignment = f.Assignments.Items.Single(x => x.Id == assignmentId);
        Assert.Equal(TrainingEffectivenessCheckStatus.Pending, assignment.EffectivenessCheckStatus);
    }

    [Fact]
    public async Task Effectiveness_pass_requires_evidence_reference()
    {
        var f = Fixture();
        var (_, assignmentId) = await Assigned(f, true, true);
        await f.Training.CompleteAsync(f.Entry.Id, assignmentId, new CompleteTrainingInput("C-1"), Corr, CancellationToken.None);
        var r = await f.Training.RecordEffectivenessAsync(f.Entry.Id, assignmentId, new RecordEffectivenessInput(true, ""), Corr, CancellationToken.None);
        Assert.False(r.IsSuccessful);
        Assert.Equal(TrainingReasonCodes.EvidenceRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Critical_process_user_ready_when_completed_and_effectiveness_passed()
    {
        var f = Fixture();
        var (_, assignmentId) = await Assigned(f, true, true);
        await f.Training.CompleteAsync(f.Entry.Id, assignmentId, new CompleteTrainingInput("C-1"), Corr, CancellationToken.None);
        await f.Training.RecordEffectivenessAsync(f.Entry.Id, assignmentId, new RecordEffectivenessInput(true, "EFF-1"), Corr, CancellationToken.None);

        var readiness = await f.Training.GetReadinessAsync(f.Entry.Id, Corr, CancellationToken.None);
        Assert.True(readiness.Data!.Ready);
    }

    [Fact]
    public async Task Critical_process_user_ready_when_formally_restricted()
    {
        var f = Fixture();
        var (_, assignmentId) = await Assigned(f, true, true);
        await f.Training.RestrictAsync(f.Entry.Id, assignmentId, new RestrictTrainingInput("supervised execution only"), Corr, CancellationToken.None);

        var readiness = await f.Training.GetReadinessAsync(f.Entry.Id, Corr, CancellationToken.None);
        Assert.True(readiness.Data!.Ready);
    }

    [Fact]
    public async Task Restriction_requires_reason()
    {
        var f = Fixture();
        var (_, assignmentId) = await Assigned(f, true, true);
        var r = await f.Training.RestrictAsync(f.Entry.Id, assignmentId, new RestrictTrainingInput(""), Corr, CancellationToken.None);
        Assert.False(r.IsSuccessful);
        Assert.Equal(TrainingReasonCodes.ReasonRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Readiness_false_when_requirement_not_assigned()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Minor);
        await AddReq(f, e.Id, false, false, mandatory: true);
        var readiness = await f.Training.GetReadinessAsync(e.Id, Corr, CancellationToken.None);
        Assert.False(readiness.Data!.Ready);
        Assert.Equal(1, readiness.Data.MissingAssignmentCount);
    }

    [Fact]
    public async Task Readiness_false_when_completion_missing()
    {
        var f = Fixture();
        await Assigned(f, false, false); // assigned but not completed
        var readiness = await f.Training.GetReadinessAsync(f.Entry.Id, Corr, CancellationToken.None);
        Assert.False(readiness.Data!.Ready);
    }

    [Fact]
    public async Task Readiness_false_when_effectiveness_pending()
    {
        var f = Fixture();
        var (_, assignmentId) = await Assigned(f, true, true);
        await f.Training.CompleteAsync(f.Entry.Id, assignmentId, new CompleteTrainingInput("C-1"), Corr, CancellationToken.None);
        var readiness = await f.Training.GetReadinessAsync(f.Entry.Id, Corr, CancellationToken.None);
        Assert.False(readiness.Data!.Ready);
        Assert.Equal(1, readiness.Data.EffectivenessPendingCount);
    }

    [Fact]
    public async Task Readiness_true_when_all_requirements_satisfied()
    {
        var f = Fixture();
        var (_, assignmentId) = await Assigned(f, false, false);
        await f.Training.CompleteAsync(f.Entry.Id, assignmentId, new CompleteTrainingInput("ACK-1"), Corr, CancellationToken.None);
        var readiness = await f.Training.GetReadinessAsync(f.Entry.Id, Corr, CancellationToken.None);
        Assert.True(readiness.Data!.Ready);
    }

    [Fact]
    public async Task Gate5_blocks_when_critical_training_matrix_missing()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical); // no matrix resolved

        var r = await f.GateEvaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        var gate5 = r.Data!.Gates.Single(g => g.GateNumber == 5);
        Assert.Equal("No", gate5.GateResult);
        Assert.Contains("TRAINING_MATRIX_MISSING", gate5.BlockingReason);
    }

    [Fact]
    public async Task Gate5_blocks_when_training_readiness_false()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical);
        await AddReq(f, e.Id, false, false, mandatory: true); // matrix exists but nothing assigned

        var decision = await f.TrainingPort.EvaluateGate5Async(f.Register.Items.Single(x => x.Id == e.Id), CancellationToken.None);
        Assert.Equal(TrainingGateOutcome.Block, decision.Outcome);
    }

    [Fact]
    public async Task Gate5_passes_when_training_readiness_true()
    {
        var f = Fixture();
        var (entry, assignmentId) = await Assigned(f, false, false);
        await f.Training.CompleteAsync(entry.Id, assignmentId, new CompleteTrainingInput("ACK-1"), Corr, CancellationToken.None);

        var r = await f.GateEvaluator.EvaluateAsync(entry.Id, Corr, CancellationToken.None);

        Assert.Equal("Yes", r.Data!.Gates.Single(g => g.GateNumber == 5).GateResult);
    }

    [Fact]
    public async Task Legacy_manual_gate5_works_for_noncritical_without_matrix()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Minor); // non-critical, no matrix → fall back → not required → auto pass

        var r = await f.GateEvaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal("Yes", r.Data!.Gates.Single(g => g.GateNumber == 5).GateResult);
    }

    [Fact]
    public async Task Cross_tenant_assignment_is_blocked()
    {
        var f = Fixture();
        var foreign = SeedEntry(f, DocumentCriticality.Minor, tenantId: OtherTenantId);

        var r = await f.Training.AssignAsync(foreign.Id, new AssignTrainingInput(Guid.NewGuid(), Learner, null, null, null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Training_records_are_never_hard_deleted()
    {
        var f = Fixture();
        var (_, assignmentId) = await Assigned(f, false, false);
        await f.Training.CompleteAsync(f.Entry.Id, assignmentId, new CompleteTrainingInput("ACK-1"), Corr, CancellationToken.None);

        Assert.DoesNotContain(f.Requirements.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Assignments.Items, x => x.IsDeleted);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<string> KeysOf(Harness f, Guid entryId) =>
        f.Requirements.Items.Where(x => x.RegisterEntryId == entryId).Select(x => x.RequirementKey).ToList();

    private static Task<Response<TrainingRequirementModel>> AddReq(Harness f, Guid entryId, bool criticalProcess, bool effectiveness, bool mandatory) =>
        f.Training.AddManualRequirementAsync(entryId, new AddManualTrainingRequirementInput(
            "Role", "DocumentOwner", null, null,
            criticalProcess ? "FullSopCompetencyAssessment" : "ReadAndUnderstand",
            criticalProcess, effectiveness, AcknowledgementRequired: !criticalProcess, mandatory), Corr, CancellationToken.None);

    private async Task<(DocumentMasterRegisterEntry Entry, Guid AssignmentId)> Assigned(Harness f, bool criticalProcess, bool effectiveness)
    {
        var req = (await AddReq(f, f.Entry.Id, criticalProcess, effectiveness, mandatory: true)).Data!;
        var assign = await f.Training.AssignAsync(f.Entry.Id, new AssignTrainingInput(req.Id, Learner, "DocumentOwner", null, null), Corr, CancellationToken.None);
        return (f.Entry, assign.Data!.Id);
    }

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var register = new FakeRegisterRepo(tenant);
        var requirements = new FakeRequirementRepo(tenant);
        var assignments = new FakeAssignmentRepo(tenant);
        var readiness = new DocumentTrainingReadinessEvaluator();
        var training = new DocumentTrainingService(register, requirements, assignments, new DocumentTrainingMatrixResolver(), readiness, tenant, new FakeUser());
        var trainingPort = new TrainingReadinessPortAdapter(requirements, assignments, readiness);
        var gateEvaluator = new DocumentReleaseGateEvaluator(register, new FakeGateEvalRepo(tenant), new FakeGateResultRepo(tenant), new FakeGateEvidenceRepo(tenant), tenant, new FakeUser(),
            Options.Create(new DocumentReleaseGateOptions()), trainingPort);

        // A default entry used by assignment helpers.
        var entry = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DocumentTitle = "Doc", DocumentClass = ControlledDocumentClass.Sop,
            DocumentType = DocumentType.Sop, Criticality = DocumentCriticality.Critical, IsControlledDocument = true,
            RegisterStatus = DocumentRegisterStatus.Active
        };
        register.Items.Add(entry);
        return new Harness(training, trainingPort, gateEvaluator, register, requirements, assignments, tenant, entry);
    }

    private static DocumentMasterRegisterEntry SeedEntry(Harness f, DocumentCriticality criticality, Guid? tenantId = null)
    {
        var e = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(), TenantId = tenantId ?? TenantId, DocumentTitle = "Document Control",
            DocumentClass = ControlledDocumentClass.Sop, DocumentType = DocumentType.Sop, Criticality = criticality,
            IsControlledDocument = true, RegisterStatus = DocumentRegisterStatus.Active
        };
        f.Register.Items.Add(e);
        return e;
    }

    private sealed record Harness(
        DocumentTrainingService Training, TrainingReadinessPortAdapter TrainingPort, DocumentReleaseGateEvaluator GateEvaluator,
        FakeRegisterRepo Register, FakeRequirementRepo Requirements, FakeAssignmentRepo Assignments, ITenantContext Tenant,
        DocumentMasterRegisterEntry Entry);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444444");
        public string? Email => "fu11@example.test";
        public string? DisplayName => "FU11 Tester";
        public string ActorName => "fu11@example.test";
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
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == entry.Id); if (i >= 0) Items[i] = entry; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeRequirementRepo(ITenantContext tenant) : IDocumentTrainingMatrixRequirementRepository
    {
        public List<DocumentTrainingMatrixRequirement> Items { get; } = [];
        private IEnumerable<DocumentTrainingMatrixRequirement> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentTrainingMatrixRequirement> CreateAsync(DocumentTrainingMatrixRequirement requirement, CancellationToken ct = default) { Items.Add(requirement); return Task.FromResult(requirement); }
        public Task<DocumentTrainingMatrixRequirement?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentTrainingMatrixRequirement>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTrainingMatrixRequirement>>(Scoped.Where(x => x.RegisterEntryId == registerEntryId).ToList());
        public Task<bool> UpdateAsync(DocumentTrainingMatrixRequirement requirement, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == requirement.Id); if (i >= 0) Items[i] = requirement; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeAssignmentRepo(ITenantContext tenant) : IDocumentTrainingAssignmentRepository
    {
        public List<DocumentTrainingAssignment> Items { get; } = [];
        private IEnumerable<DocumentTrainingAssignment> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentTrainingAssignment> CreateAsync(DocumentTrainingAssignment assignment, CancellationToken ct = default) { Items.Add(assignment); return Task.FromResult(assignment); }
        public Task<DocumentTrainingAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentTrainingAssignment>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTrainingAssignment>>(Scoped.Where(x => x.RegisterEntryId == registerEntryId).ToList());
        public Task<IReadOnlyList<DocumentTrainingAssignment>> GetByRequirementAsync(Guid requirementId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTrainingAssignment>>(Scoped.Where(x => x.RequirementId == requirementId).ToList());
        public Task<bool> UpdateAsync(DocumentTrainingAssignment assignment, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == assignment.Id); if (i >= 0) Items[i] = assignment; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeGateEvalRepo(ITenantContext tenant) : IDocumentReleaseGateEvaluationRepository
    {
        public List<DocumentReleaseGateEvaluation> Items { get; } = [];
        public Task<DocumentReleaseGateEvaluation> CreateAsync(DocumentReleaseGateEvaluation evaluation, CancellationToken ct = default) { Items.Add(evaluation); return Task.FromResult(evaluation); }
        public Task<DocumentReleaseGateEvaluation?> GetLatestAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == registerEntryId).OrderByDescending(x => x.EvaluatedAt).FirstOrDefault());
        public Task<IReadOnlyList<DocumentReleaseGateEvaluation>> GetHistoryAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentReleaseGateEvaluation>>(Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == registerEntryId).ToList());
    }

    private sealed class FakeGateResultRepo(ITenantContext tenant) : IDocumentReleaseGateResultRepository
    {
        public List<DocumentReleaseGateResult> Items { get; } = [];
        public Task<DocumentReleaseGateResult> CreateAsync(DocumentReleaseGateResult result, CancellationToken ct = default) { Items.Add(result); return Task.FromResult(result); }
        public Task<IReadOnlyList<DocumentReleaseGateResult>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentReleaseGateResult>>(Items.Where(x => x.TenantId == tenant.TenantId && x.EvaluationId == evaluationId).OrderBy(x => x.GateNumber).ToList());
    }

    private sealed class FakeGateEvidenceRepo(ITenantContext tenant) : IDocumentReleaseGateEvidenceRepository
    {
        public List<DocumentReleaseGateEvidence> Items { get; } = [];
        public Task<DocumentReleaseGateEvidence> CreateAsync(DocumentReleaseGateEvidence evidence, CancellationToken ct = default) { Items.Add(evidence); return Task.FromResult(evidence); }
        public Task<IReadOnlyList<DocumentReleaseGateEvidence>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentReleaseGateEvidence>>(Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == registerEntryId).ToList());
        public Task<DocumentReleaseGateEvidence?> GetLatestForGateAsync(Guid registerEntryId, ReleaseGateKey gateKey, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == registerEntryId && x.GateKey == gateKey).OrderByDescending(x => x.VerificationDate).FirstOrDefault());
    }
}
