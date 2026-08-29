using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementControlledCopy;
using Diten.Platform.Application.Features.DocumentManagementControlledCopy.Services;
using Diten.Platform.Application.Features.DocumentManagementLifecycle;
using Diten.Platform.Application.Features.DocumentManagementLifecycle.Services;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates.Services;
using Diten.Platform.Application.Features.DocumentManagementSuspension;
using Diten.Platform.Application.Features.DocumentManagementSuspension.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU17 — controlled copy / obsolete reconciliation tests. Tenant-aware in-memory fakes exercise the copy log,
/// withdrawal plans, obsolete findings, FU10 Gate 6 computed behaviour and the FU13 withdrawal seam.
/// </summary>
public sealed class DocumentControlledCopyTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private const string Corr = "fu17-corr-1";

    // ── controlled copies ─────────────────────────────────────────────────────

    [Fact]
    public async Task Register_active_digital_controlled_copy_for_effective_document()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective);

        var r = await f.Service.RegisterCopyAsync(e.Id, Copy("DigitalControlledCopy"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Active", r.Data!.CopyStatus);
        Assert.Equal(1, r.Data.CopyNumber);
    }

    [Fact]
    public async Task Register_active_copy_blocked_for_suspended_document()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Suspended);
        var r = await f.Service.RegisterCopyAsync(e.Id, Copy("DigitalControlledCopy"), Corr, CancellationToken.None);
        Assert.False(r.IsSuccessful);
        Assert.Equal(ControlledCopyReasonCodes.NotEligibleForActiveCopy, r.ReasonCode);
    }

    [Fact]
    public async Task Register_active_copy_blocked_for_retired_document()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Retired);
        var r = await f.Service.RegisterCopyAsync(e.Id, Copy("DigitalControlledCopy"), Corr, CancellationToken.None);
        Assert.False(r.IsSuccessful);
        Assert.Equal(ControlledCopyReasonCodes.NotEligibleForActiveCopy, r.ReasonCode);
    }

    [Fact]
    public async Task CopyNumber_unique_per_register()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective);
        await f.Service.RegisterCopyAsync(e.Id, Copy("DigitalControlledCopy") with { CopyNumber = 5 }, Corr, CancellationToken.None);

        var dup = await f.Service.RegisterCopyAsync(e.Id, Copy("DigitalControlledCopy") with { CopyNumber = 5 }, Corr, CancellationToken.None);

        Assert.False(dup.IsSuccessful);
        Assert.Equal(ControlledCopyReasonCodes.DuplicateCopyNumber, dup.ReasonCode);
    }

    [Fact]
    public async Task Printed_copy_requires_location_or_holder()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective);

        var r = await f.Service.RegisterCopyAsync(e.Id, new RegisterControlledCopyInput("PrintedControlledCopy", null, "PointOfUse", null, null, null, null, null, null, null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(ControlledCopyReasonCodes.HolderOrLocationRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Withdraw_copy_requires_evidence()
    {
        var f = Fixture();
        var (e, copyId) = await ActiveCopy(f);

        var r = await f.Service.WithdrawAsync(e.Id, copyId, new WithdrawControlledCopyInput(""), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(ControlledCopyReasonCodes.EvidenceRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Mark_copy_withdrawn_sets_status_and_evidence()
    {
        var f = Fixture();
        var (e, copyId) = await ActiveCopy(f);

        var r = await f.Service.WithdrawAsync(e.Id, copyId, new WithdrawControlledCopyInput("WD-1"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Withdrawn", r.Data!.CopyStatus);
        Assert.Equal("WD-1", r.Data.WithdrawalEvidenceReference);
    }

    [Fact]
    public async Task Reconcile_copy_requires_evidence()
    {
        var f = Fixture();
        var (e, copyId) = await ActiveCopy(f);

        var r = await f.Service.ReconcileAsync(e.Id, copyId, new ReconcileControlledCopyInput(""), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(ControlledCopyReasonCodes.EvidenceRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Mark_copy_obsolete_creates_finding()
    {
        var f = Fixture();
        var (e, copyId) = await ActiveCopy(f);

        var r = await f.Service.MarkObsoleteAsync(e.Id, copyId, new MarkControlledCopyObsoleteInput("found at point of use", "Line 3"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Obsolete", r.Data!.CopyStatus);
        Assert.Contains(f.Findings.Items, x => x.ControlledCopyId == copyId && x.FindingType == ObsoleteCopyFindingType.UncontrolledCopyDetected);
    }

    // ── obsolete reconciliation ───────────────────────────────────────────────

    [Fact]
    public async Task Superseded_document_with_active_copy_creates_obsolete_finding()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective);
        await f.Service.RegisterCopyAsync(e.Id, Copy("DigitalControlledCopy"), Corr, CancellationToken.None);
        e.LifecycleStatus = ControlledDocumentLifecycleStatus.Superseded;

        await f.Service.EvaluateReconciliationAsync(e.Id, Corr, CancellationToken.None);

        Assert.Contains(f.Findings.Items, x => x.FindingType == ObsoleteCopyFindingType.SupersededCopyAtPointOfUse && x.Severity == ObsoleteCopyFindingSeverity.Major);
    }

    [Fact]
    public async Task Suspended_document_with_active_copy_creates_critical_finding()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective);
        await f.Service.RegisterCopyAsync(e.Id, Copy("DigitalControlledCopy"), Corr, CancellationToken.None);
        e.LifecycleStatus = ControlledDocumentLifecycleStatus.Suspended;

        await f.Service.EvaluateReconciliationAsync(e.Id, Corr, CancellationToken.None);

        Assert.Contains(f.Findings.Items, x => x.FindingType == ObsoleteCopyFindingType.SuspendedDocumentInUse && x.Severity == ObsoleteCopyFindingSeverity.Critical);
    }

    // ── withdrawal plan ───────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_withdrawal_plan_from_active_copies_marks_them_pending()
    {
        var f = Fixture();
        var (e, copyId) = await ActiveCopy(f);

        var plan = await f.Service.GeneratePlanAsync(e.Id, new GenerateWithdrawalPlanInput("Superseded", null), Corr, CancellationToken.None);

        Assert.True(plan.IsSuccessful);
        Assert.Equal(1, plan.Data!.RequiredCopyCount);
        Assert.Equal("Active", plan.Data.PlanStatus);
        Assert.Equal(ControlledCopyStatus.PendingWithdrawal, f.Copies.Items.Single(x => x.Id == copyId).CopyStatus);
    }

    [Fact]
    public async Task Complete_withdrawal_plan_blocks_when_copy_not_withdrawn()
    {
        var f = Fixture();
        var (e, _) = await ActiveCopy(f);
        var plan = await f.Service.GeneratePlanAsync(e.Id, new GenerateWithdrawalPlanInput("Superseded", null), Corr, CancellationToken.None);

        var r = await f.Service.CompletePlanAsync(e.Id, plan.Data!.Id, new CompleteWithdrawalPlanInput(null, null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(ControlledCopyReasonCodes.PlanIncomplete, r.ReasonCode);
    }

    [Fact]
    public async Task Complete_withdrawal_plan_passes_when_all_copies_withdrawn()
    {
        var f = Fixture();
        var (e, copyId) = await ActiveCopy(f);
        var plan = await f.Service.GeneratePlanAsync(e.Id, new GenerateWithdrawalPlanInput("Superseded", null), Corr, CancellationToken.None);
        await f.Service.WithdrawAsync(e.Id, copyId, new WithdrawControlledCopyInput("WD-1"), Corr, CancellationToken.None);

        var r = await f.Service.CompletePlanAsync(e.Id, plan.Data!.Id, new CompleteWithdrawalPlanInput("PLAN-EV-1", null), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Completed", r.Data!.PlanStatus);
    }

    [Fact]
    public async Task Missing_copy_requires_deviation_reference_to_complete_plan()
    {
        var f = Fixture();
        var (e, copyId) = await ActiveCopy(f);
        var plan = await f.Service.GeneratePlanAsync(e.Id, new GenerateWithdrawalPlanInput("Superseded", null), Corr, CancellationToken.None);
        await f.Service.MarkMissingAsync(e.Id, copyId, new MarkControlledCopyMissingInput("not found"), Corr, CancellationToken.None);

        var missing = await f.Service.CompletePlanAsync(e.Id, plan.Data!.Id, new CompleteWithdrawalPlanInput(null, null), Corr, CancellationToken.None);
        Assert.False(missing.IsSuccessful);
        Assert.Equal(ControlledCopyReasonCodes.DeviationRequired, missing.ReasonCode);

        var ok = await f.Service.CompletePlanAsync(e.Id, plan.Data.Id, new CompleteWithdrawalPlanInput(null, "DEV-1"), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
    }

    // ── FU10 Gate 6 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Gate6_blocks_when_active_obsolete_copy_exists()
    {
        var f = Fixture();
        var (e, copyId) = await ActiveCopy(f, DocumentCriticality.Critical);
        await f.Service.MarkObsoleteAsync(e.Id, copyId, new MarkControlledCopyObsoleteInput("obsolete in use", null), Corr, CancellationToken.None);

        var r = await f.GateEvaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal("No", Gate(r, 6).GateResult);
    }

    [Fact]
    public async Task Gate6_blocks_when_withdrawal_plan_incomplete()
    {
        var f = Fixture();
        var (e, _) = await ActiveCopy(f, DocumentCriticality.Critical);
        await f.Service.GeneratePlanAsync(e.Id, new GenerateWithdrawalPlanInput("Superseded", null), Corr, CancellationToken.None);

        var r = await f.GateEvaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal("No", Gate(r, 6).GateResult);
    }

    [Fact]
    public async Task Gate6_passes_when_withdrawal_plan_complete()
    {
        var f = Fixture();
        var (e, copyId) = await ActiveCopy(f, DocumentCriticality.Critical);
        var plan = await f.Service.GeneratePlanAsync(e.Id, new GenerateWithdrawalPlanInput("Superseded", null), Corr, CancellationToken.None);
        await f.Service.WithdrawAsync(e.Id, copyId, new WithdrawControlledCopyInput("WD-1"), Corr, CancellationToken.None);
        await f.Service.CompletePlanAsync(e.Id, plan.Data!.Id, new CompleteWithdrawalPlanInput("PLAN-EV-1", null), Corr, CancellationToken.None);

        var r = await f.GateEvaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal("Yes", Gate(r, 6).GateResult);
    }

    [Fact]
    public async Task Gate6_legacy_manual_evidence_works_for_noncritical_without_copy_data()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.ApprovedPendingEffective, DocumentCriticality.Minor);
        f.GateEvidence.Items.Add(new DocumentReleaseGateEvidence
        {
            Id = Guid.NewGuid(), TenantId = TenantId, RegisterEntryId = e.Id, GateKey = ReleaseGateKey.SupersededCopyWithdrawalMethod,
            EvidenceReference = "MANUAL-WD-1", VerifiedByUserId = Guid.NewGuid(), VerifiedByRole = "LocalQA", VerificationDate = DateTimeOffset.UtcNow
        });

        var r = await f.GateEvaluator.EvaluateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal("Yes", Gate(r, 6).GateResult);
    }

    // ── FU13 seam ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task FU13_suspension_execution_triggers_withdrawal_plan_when_port_available()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");
        await f.Service.RegisterCopyAsync(e.Id, Copy("DigitalControlledCopy"), Corr, CancellationToken.None);
        var suspension = SuspensionService(f);
        var opened = await suspension.OpenAsync(e.Id, new OpenSuspensionCaseInput("QualityRisk", "risk", null), Corr, CancellationToken.None);
        await suspension.ApproveAsync(e.Id, opened.Data!.Id, new ApproveSuspensionInput("Suspend", "risk", "GQD", "COMM-1"), Corr, CancellationToken.None);

        await suspension.ExecuteAsync(e.Id, opened.Data.Id, new ExecuteSuspensionInput("NOTICE-1", "ACCESS-1", "AFFECTED-1"), Corr, CancellationToken.None);

        Assert.NotEmpty(f.Plans.Items.Where(x => x.RegisterEntryId == e.Id));
    }

    [Fact]
    public async Task FU13_retirement_execution_triggers_withdrawal_plan_when_port_available()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");
        await f.Service.RegisterCopyAsync(e.Id, Copy("DigitalControlledCopy"), Corr, CancellationToken.None);
        var retirement = RetirementService(f);
        var req = await retirement.RequestAsync(e.Id, new RequestRetirementInput("obsolete", "JUST-1", "TRANS-1", null, null), Corr, CancellationToken.None);
        await retirement.ApproveAsync(e.Id, req.Data!.Id, new ApproveRetirementInput("GQD"), Corr, CancellationToken.None);

        await retirement.ExecuteAsync(e.Id, req.Data.Id, new ExecuteRetirementInput("COMM-1", "ARCH-1"), Corr, CancellationToken.None);

        Assert.NotEmpty(f.Plans.Items.Where(x => x.RegisterEntryId == e.Id));
    }

    // ── isolation / durability ────────────────────────────────────────────────

    [Fact]
    public async Task Cross_tenant_controlled_copy_is_blocked()
    {
        var f = Fixture();
        var foreign = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective, tenantId: OtherTenantId);

        var r = await f.Service.RegisterCopyAsync(foreign.Id, Copy("DigitalControlledCopy"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Copies_plans_and_findings_are_never_hard_deleted()
    {
        var f = Fixture();
        var (e, copyId) = await ActiveCopy(f);
        await f.Service.MarkObsoleteAsync(e.Id, copyId, new MarkControlledCopyObsoleteInput("obsolete", null), Corr, CancellationToken.None);
        await f.Service.GeneratePlanAsync(e.Id, new GenerateWithdrawalPlanInput("Manual", null), Corr, CancellationToken.None);

        Assert.DoesNotContain(f.Copies.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Plans.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Findings.Items, x => x.IsDeleted);
        Assert.NotEmpty(f.Copies.Items);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static ReleaseGateResultModel Gate(Response<ReleaseGateEvaluationModel> r, int gateNumber) =>
        r.Data!.Gates.Single(g => g.GateNumber == gateNumber);

    private static RegisterControlledCopyInput Copy(string type) =>
        new(type, null, "PointOfUse", "Line 1 binder", null, null, null, null, null, null);

    private async Task<(DocumentMasterRegisterEntry Entry, Guid CopyId)> ActiveCopy(Harness f, DocumentCriticality criticality = DocumentCriticality.Major)
    {
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective, criticality);
        var copy = await f.Service.RegisterCopyAsync(e.Id, Copy("DigitalControlledCopy"), Corr, CancellationToken.None);
        return (e, copy.Data!.Id);
    }

    private static DocumentSuspensionService SuspensionService(Harness f)
    {
        var lifecycle = new DocumentLifecycleService(f.Register, new FakeTransitionRepo(f.Tenant), f.Tenant, new FakeUser(), Options.Create(new DocumentLifecycleOptions()));
        var adapter = new ControlledCopyWithdrawalPortAdapter(f.Service);
        return new DocumentSuspensionService(f.Register, new FakeSuspensionCaseRepo(f.Tenant), new FakeEscalationRepo(f.Tenant), lifecycle, f.Tenant, new FakeUser(), adapter);
    }

    private static DocumentRetirementService RetirementService(Harness f)
    {
        var lifecycle = new DocumentLifecycleService(f.Register, new FakeTransitionRepo(f.Tenant), f.Tenant, new FakeUser(), Options.Create(new DocumentLifecycleOptions()));
        var adapter = new ControlledCopyWithdrawalPortAdapter(f.Service);
        return new DocumentRetirementService(f.Register, new FakeRetirementCaseRepo(f.Tenant), new FakeSuspensionCaseRepo(f.Tenant), lifecycle, f.Tenant, new FakeUser(), adapter);
    }

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var register = new FakeRegisterRepo(tenant);
        var copies = new FakeCopyRepo(tenant);
        var plans = new FakePlanRepo(tenant);
        var findings = new FakeFindingRepo(tenant);
        var readiness = new DocumentControlledCopyReadinessEvaluator();
        var service = new DocumentControlledCopyService(register, copies, plans, findings, readiness, tenant, new FakeUser());
        var copyPort = new CopyReconciliationPortAdapter(copies, plans, findings, readiness);
        var gateEvidence = new FakeGateEvidenceRepo(tenant);
        var gateEvaluator = new DocumentReleaseGateEvaluator(register, new FakeGateEvalRepo(tenant), new FakeGateResultRepo(tenant), gateEvidence,
            tenant, new FakeUser(), Options.Create(new DocumentReleaseGateOptions()), trainingPort: null, repositoryPort: null, copyPort: copyPort);
        return new Harness(service, gateEvaluator, register, copies, plans, findings, gateEvidence, tenant);
    }

    private static DocumentMasterRegisterEntry SeedEntry(
        Harness f, ControlledDocumentLifecycleStatus status, DocumentCriticality criticality = DocumentCriticality.Major,
        string? uid = null, string? code = null, Guid? tenantId = null)
    {
        var e = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(), TenantId = tenantId ?? TenantId, DocumentTitle = "Document Control",
            DocumentClass = ControlledDocumentClass.Sop, DocumentType = DocumentType.Sop, Criticality = criticality,
            IsControlledDocument = true, LifecycleStatus = status, RegisterStatus = DocumentRegisterStatus.Active,
            PermanentUid = uid, DocumentCode = code, EffectiveDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        f.Register.Items.Add(e);
        return e;
    }

    private sealed record Harness(
        DocumentControlledCopyService Service, DocumentReleaseGateEvaluator GateEvaluator, FakeRegisterRepo Register,
        FakeCopyRepo Copies, FakePlanRepo Plans, FakeFindingRepo Findings, FakeGateEvidenceRepo GateEvidence, ITenantContext Tenant);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444444");
        public string? Email => "fu17@example.test";
        public string? DisplayName => "FU17 Tester";
        public string ActorName => "fu17@example.test";
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
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default)
        {
            var q = Scoped;
            if (filter.LifecycleStatus is { } ls) q = q.Where(x => x.LifecycleStatus == ls);
            return Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(q.ToList());
        }
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry e, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == e.Id); if (i >= 0) Items[i] = e; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeCopyRepo(ITenantContext tenant) : IDocumentControlledCopyRepository
    {
        public List<DocumentControlledCopy> Items { get; } = [];
        private IEnumerable<DocumentControlledCopy> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentControlledCopy> CreateAsync(DocumentControlledCopy c, CancellationToken ct = default) { Items.Add(c); return Task.FromResult(c); }
        public Task<DocumentControlledCopy?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentControlledCopy>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentControlledCopy>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<DocumentControlledCopy?> GetByCopyNumberAsync(Guid id, int n, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.RegisterEntryId == id && x.CopyNumber == n));
        public Task<bool> UpdateAsync(DocumentControlledCopy c, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == c.Id); if (i >= 0) Items[i] = c; return Task.FromResult(i >= 0); }
    }

    private sealed class FakePlanRepo(ITenantContext tenant) : IDocumentCopyWithdrawalPlanRepository
    {
        public List<DocumentCopyWithdrawalPlan> Items { get; } = [];
        private IEnumerable<DocumentCopyWithdrawalPlan> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentCopyWithdrawalPlan> CreateAsync(DocumentCopyWithdrawalPlan p, CancellationToken ct = default) { Items.Add(p); return Task.FromResult(p); }
        public Task<DocumentCopyWithdrawalPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentCopyWithdrawalPlan>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentCopyWithdrawalPlan>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<DocumentCopyWithdrawalPlan?> GetOpenAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.Where(x => x.RegisterEntryId == id && x.PlanStatus != CopyWithdrawalPlanStatus.Completed && x.PlanStatus != CopyWithdrawalPlanStatus.Cancelled).OrderByDescending(x => x.CreatedAt).FirstOrDefault());
        public Task<bool> UpdateAsync(DocumentCopyWithdrawalPlan p, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == p.Id); if (i >= 0) Items[i] = p; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeFindingRepo(ITenantContext tenant) : IDocumentObsoleteCopyFindingRepository
    {
        public List<DocumentObsoleteCopyFinding> Items { get; } = [];
        private IEnumerable<DocumentObsoleteCopyFinding> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentObsoleteCopyFinding> CreateAsync(DocumentObsoleteCopyFinding f, CancellationToken ct = default) { Items.Add(f); return Task.FromResult(f); }
        public Task<DocumentObsoleteCopyFinding?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentObsoleteCopyFinding>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentObsoleteCopyFinding>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<bool> UpdateAsync(DocumentObsoleteCopyFinding f, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == f.Id); if (i >= 0) Items[i] = f; return Task.FromResult(i >= 0); }
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

    private sealed class FakeTransitionRepo(ITenantContext tenant) : IDocumentLifecycleTransitionRecordRepository
    {
        public List<DocumentLifecycleTransitionRecord> Items { get; } = [];
        public Task<DocumentLifecycleTransitionRecord> CreateAsync(DocumentLifecycleTransitionRecord r, CancellationToken ct = default) { Items.Add(r); return Task.FromResult(r); }
        public Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentLifecycleTransitionRecord>>(Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == id).ToList());
        public Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentLifecycleTransitionRecord>>(Items.Where(x => x.TenantId == tenant.TenantId).ToList());
    }

    private sealed class FakeSuspensionCaseRepo(ITenantContext tenant) : IDocumentSuspensionCaseRepository
    {
        public List<DocumentSuspensionCase> Items { get; } = [];
        private IEnumerable<DocumentSuspensionCase> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentSuspensionCase> CreateAsync(DocumentSuspensionCase c, CancellationToken ct = default) { Items.Add(c); return Task.FromResult(c); }
        public Task<DocumentSuspensionCase?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentSuspensionCase>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentSuspensionCase>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<DocumentSuspensionCase?> GetOpenAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.Where(x => x.RegisterEntryId == id && x.CaseStatus != SuspensionCaseStatus.Closed && x.CaseStatus != SuspensionCaseStatus.Cancelled && x.CaseStatus != SuspensionCaseStatus.Rejected).OrderByDescending(x => x.CaseNumber).FirstOrDefault());
        public Task<bool> UpdateAsync(DocumentSuspensionCase c, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == c.Id); if (i >= 0) Items[i] = c; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeRetirementCaseRepo(ITenantContext tenant) : IDocumentRetirementCaseRepository
    {
        public List<DocumentRetirementCase> Items { get; } = [];
        private IEnumerable<DocumentRetirementCase> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentRetirementCase> CreateAsync(DocumentRetirementCase c, CancellationToken ct = default) { Items.Add(c); return Task.FromResult(c); }
        public Task<DocumentRetirementCase?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentRetirementCase>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentRetirementCase>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<bool> UpdateAsync(DocumentRetirementCase c, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == c.Id); if (i >= 0) Items[i] = c; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeEscalationRepo(ITenantContext tenant) : IDocumentPeriodicReviewEscalationRepository
    {
        public List<DocumentPeriodicReviewEscalation> Items { get; } = [];
        public Task<DocumentPeriodicReviewEscalation> CreateAsync(DocumentPeriodicReviewEscalation e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<IReadOnlyList<DocumentPeriodicReviewEscalation>> GetByReviewAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentPeriodicReviewEscalation>>(Items.Where(x => x.TenantId == tenant.TenantId && x.PeriodicReviewId == id).ToList());
        public Task<IReadOnlyList<DocumentPeriodicReviewEscalation>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentPeriodicReviewEscalation>>(Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == id).ToList());
    }
}
