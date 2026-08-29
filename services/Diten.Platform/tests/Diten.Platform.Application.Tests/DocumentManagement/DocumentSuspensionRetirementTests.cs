using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementLifecycle;
using Diten.Platform.Application.Features.DocumentManagementLifecycle.Services;
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
/// MOD-0029-FU13 — suspension / urgent withdrawal / retirement / temporary-instruction tests. Tenant-aware in-memory
/// fakes exercise the SOP §12.1 chain, §9.16 retirement evidence, the §6.1 30-day temporary ceiling and expiry
/// actions. Lifecycle changes are delegated to the real FU08 engine (no matrix duplication).
/// </summary>
public sealed class DocumentSuspensionRetirementTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private const string Corr = "fu13-corr-1";

    // ── suspension ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Open_suspension_case_for_effective_document()
    {
        var f = Fixture();
        var e = SeedEntry(f);

        var r = await f.Suspension.OpenAsync(e.Id, Open("QualityRisk", "contamination risk identified"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Opened", r.Data!.CaseStatus);
        Assert.NotNull(r.Data.QaNotifiedAt); // SOP §12.1: user stops use and notifies QA immediately.
    }

    [Fact]
    public async Task Open_suspension_case_links_periodic_review_escalation()
    {
        var f = Fixture();
        var e = SeedEntry(f);
        var escalation = SeedEscalation(f, e.Id);

        var r = await f.Suspension.OpenAsync(e.Id, Open("PeriodicReviewOverdue", "critical review overdue", escalation.Id), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(escalation.Id, r.Data!.SourcePeriodicReviewEscalationId);
    }

    [Fact]
    public async Task Open_suspension_case_is_idempotent()
    {
        var f = Fixture();
        var e = SeedEntry(f);
        var first = await f.Suspension.OpenAsync(e.Id, Open("QualityRisk", "risk"), Corr, CancellationToken.None);

        var second = await f.Suspension.OpenAsync(e.Id, Open("QualityRisk", "risk again"), Corr, CancellationToken.None);

        Assert.Equal(first.Data!.Id, second.Data!.Id);
        Assert.Single(f.Cases.Items);
    }

    [Fact]
    public async Task Approve_suspension_requires_GQD_or_independent_QA()
    {
        var f = Fixture();
        var (e, caseId) = await Opened(f);

        var bad = await f.Suspension.ApproveAsync(e.Id, caseId, Approve(role: "QADocumentation"), Corr, CancellationToken.None);
        Assert.False(bad.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.ApproverRoleInvalid, bad.ReasonCode);

        var ok = await f.Suspension.ApproveAsync(e.Id, caseId, Approve(role: "GQD"), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
    }

    [Fact]
    public async Task Approve_suspension_requires_communication_plan()
    {
        var f = Fixture();
        var (e, caseId) = await Opened(f);

        var r = await f.Suspension.ApproveAsync(e.Id, caseId,
            new ApproveSuspensionInput("Suspend", "risk", "GQD", ""), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.CommunicationPlanRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Execute_suspension_requires_access_removal_notice_and_affected_records_evidence()
    {
        var f = Fixture();
        var (e, caseId) = await Approved(f);

        var r = await f.Suspension.ExecuteAsync(e.Id, caseId, new ExecuteSuspensionInput("NOTICE-1", "", "AFFECTED-1"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.EvidenceRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Execute_suspension_transitions_lifecycle_to_Suspended()
    {
        var f = Fixture();
        var (e, caseId) = await Approved(f);

        var r = await f.Suspension.ExecuteAsync(e.Id, caseId, Execute(), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Executed", r.Data!.CaseStatus);
        Assert.Equal(ControlledDocumentLifecycleStatus.Suspended, f.Register.Items.Single(x => x.Id == e.Id).LifecycleStatus);
    }

    [Fact]
    public async Task Execute_suspension_requires_approved_case_with_suspend_decision()
    {
        var f = Fixture();
        var (e, caseId) = await Opened(f); // not approved

        var r = await f.Suspension.ExecuteAsync(e.Id, caseId, Execute(), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.CaseNotApproved, r.ReasonCode);
    }

    [Fact]
    public async Task Execute_suspension_does_not_delete_document_or_identifiers()
    {
        var f = Fixture();
        var (e, caseId) = await Approved(f);

        await f.Suspension.ExecuteAsync(e.Id, caseId, Execute(), Corr, CancellationToken.None);

        var after = f.Register.Items.Single(x => x.Id == e.Id);
        Assert.False(after.IsDeleted);
        Assert.Equal("UID-0000001", after.PermanentUid);
        Assert.Equal("GMG-QMS-SOP-0001", after.DocumentCode);
    }

    [Fact]
    public async Task Close_suspension_case_requires_deviation_or_corrective_action_for_quality_risk()
    {
        var f = Fixture();
        var (e, caseId) = await Approved(f); // trigger is QualityRisk

        var missing = await f.Suspension.CloseAsync(e.Id, caseId, new CloseSuspensionCaseInput(null, null, null), Corr, CancellationToken.None);
        Assert.False(missing.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.DeviationRequired, missing.ReasonCode);

        var ok = await f.Suspension.CloseAsync(e.Id, caseId, new CloseSuspensionCaseInput("DEV-1", null, null), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.NotEmpty(ok.Data!.Warnings); // no replacement plan recorded
    }

    [Fact]
    public async Task Suspended_document_cannot_open_another_suspension_case()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Suspended);

        var r = await f.Suspension.OpenAsync(e.Id, Open("QualityRisk", "risk"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.NotEligible, r.ReasonCode);
    }

    // ── retirement ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Request_retirement_requires_justification_and_transition_assessment()
    {
        var f = Fixture();
        var e = SeedEntry(f);

        var r = await f.Retirement.RequestAsync(e.Id, new RequestRetirementInput("obsolete", "", "", null, null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.EvidenceRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Approve_retirement_records_approval_and_requires_permitted_role()
    {
        var f = Fixture();
        var (e, caseId) = await RetirementRequested(f);

        var bad = await f.Retirement.ApproveAsync(e.Id, caseId, new ApproveRetirementInput("QADocumentation"), Corr, CancellationToken.None);
        Assert.False(bad.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.ApproverRoleInvalid, bad.ReasonCode);

        var ok = await f.Retirement.ApproveAsync(e.Id, caseId, new ApproveRetirementInput("GQD"), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Equal("Approved", ok.Data!.CaseStatus);
        Assert.NotNull(ok.Data.ApprovedAt);
    }

    [Fact]
    public async Task Execute_retirement_requires_communication_and_archival_evidence()
    {
        var f = Fixture();
        var (e, caseId) = await RetirementApproved(f);

        var r = await f.Retirement.ExecuteAsync(e.Id, caseId, new ExecuteRetirementInput("COMM-1", ""), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.EvidenceRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Execute_retirement_transitions_lifecycle_to_Retired()
    {
        var f = Fixture();
        var (e, caseId) = await RetirementApproved(f);

        var r = await f.Retirement.ExecuteAsync(e.Id, caseId, new ExecuteRetirementInput("COMM-1", "ARCH-1"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(ControlledDocumentLifecycleStatus.Retired, f.Register.Items.Single(x => x.Id == e.Id).LifecycleStatus);
    }

    [Fact]
    public async Task Retired_document_uid_and_code_are_retained_and_never_reused()
    {
        var f = Fixture();
        var (e, caseId) = await RetirementApproved(f);

        await f.Retirement.ExecuteAsync(e.Id, caseId, new ExecuteRetirementInput("COMM-1", "ARCH-1"), Corr, CancellationToken.None);

        var after = f.Register.Items.Single(x => x.Id == e.Id);
        Assert.Equal("UID-0000001", after.PermanentUid);   // FU07 invariant: never cleared, never freed
        Assert.Equal("GMG-QMS-SOP-0001", after.DocumentCode);
        Assert.False(after.IsDeleted);
    }

    [Fact]
    public async Task Retirement_of_suspended_document_is_allowed()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Suspended);
        var req = await f.Retirement.RequestAsync(e.Id, Request(), Corr, CancellationToken.None);
        await f.Retirement.ApproveAsync(e.Id, req.Data!.Id, new ApproveRetirementInput("GQD"), Corr, CancellationToken.None);

        var r = await f.Retirement.ExecuteAsync(e.Id, req.Data.Id, new ExecuteRetirementInput("COMM-1", "ARCH-1"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(ControlledDocumentLifecycleStatus.Retired, f.Register.Items.Single(x => x.Id == e.Id).LifecycleStatus);
    }

    // ── MOD-0029-FU08A — UnderRevision suspension / retirement now flow through FU08. ──────────────────────

    [Fact]
    public async Task Execute_suspension_for_under_revision_document_succeeds()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.UnderRevision);
        var opened = await f.Suspension.OpenAsync(e.Id, Open("QualityRisk", "risk during revision"), Corr, CancellationToken.None);
        await f.Suspension.ApproveAsync(e.Id, opened.Data!.Id, Approve("GQD"), Corr, CancellationToken.None);

        var r = await f.Suspension.ExecuteAsync(e.Id, opened.Data.Id, Execute(), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(ControlledDocumentLifecycleStatus.Suspended, f.Register.Items.Single(x => x.Id == e.Id).LifecycleStatus);
    }

    [Fact]
    public async Task Execute_retirement_for_under_revision_document_succeeds()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.UnderRevision);
        var req = await f.Retirement.RequestAsync(e.Id, Request(), Corr, CancellationToken.None);
        await f.Retirement.ApproveAsync(e.Id, req.Data!.Id, new ApproveRetirementInput("GQD"), Corr, CancellationToken.None);

        var r = await f.Retirement.ExecuteAsync(e.Id, req.Data.Id, new ExecuteRetirementInput("COMM-1", "ARCH-1"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(ControlledDocumentLifecycleStatus.Retired, f.Register.Items.Single(x => x.Id == e.Id).LifecycleStatus);
    }

    // ── temporary instruction ─────────────────────────────────────────────────

    [Fact]
    public async Task Start_temporary_instruction_rejects_validity_over_30_days()
    {
        var f = Fixture();
        var e = SeedTemporary(f);

        var r = await f.Temporary.StartAsync(e.Id, new StartTemporaryInstructionInput(null, DateTimeOffset.UtcNow.AddDays(31)), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.TemporaryValidityExceeded, r.ReasonCode);
    }

    [Fact]
    public async Task Start_temporary_instruction_rejects_non_temporary_document()
    {
        var f = Fixture();
        var e = SeedEntry(f); // ordinary SOP

        var r = await f.Temporary.StartAsync(e.Id, new StartTemporaryInstructionInput(null, DateTimeOffset.UtcNow.AddDays(10)), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.NotTemporaryInstruction, r.ReasonCode);
    }

    [Fact]
    public async Task Temporary_instruction_due_to_expire_detected()
    {
        var f = Fixture();
        var e = SeedTemporary(f);
        await f.Temporary.StartAsync(e.Id, new StartTemporaryInstructionInput(null, DateTimeOffset.UtcNow.AddDays(3)), Corr, CancellationToken.None);

        var r = await f.Temporary.EvaluateExpiryAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal(nameof(TemporaryInstructionStatus.DueToExpire), r.Data!.TemporaryInstructionStatus);
        Assert.NotEmpty(r.Data.Warnings);
    }

    [Fact]
    public async Task Expired_temporary_instruction_without_action_creates_suspension_case()
    {
        var f = Fixture();
        var e = SeedTemporary(f);
        await f.Temporary.StartAsync(e.Id, new StartTemporaryInstructionInput(null, DateTimeOffset.UtcNow.AddDays(5)), Corr, CancellationToken.None);
        // Simulate expiry.
        f.Controls.Items.Single().ValidUntil = DateTimeOffset.UtcNow.AddDays(-1);

        var r = await f.Temporary.EvaluateExpiryAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal(nameof(TemporaryInstructionStatus.Expired), r.Data!.TemporaryInstructionStatus);
        Assert.NotNull(r.Data.SuspensionCaseId); // shall never remain operational by default
        Assert.Single(f.Cases.Items);
    }

    [Fact]
    public async Task Close_temporary_instruction_with_incorporate_action_requires_evidence()
    {
        var f = Fixture();
        var e = await StartedTemporary(f);

        var missing = await f.Temporary.CloseAsync(e.Id, new CloseTemporaryInstructionInput("IncorporateIntoPermanent", null, null), Corr, CancellationToken.None);
        Assert.False(missing.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.EvidenceRequired, missing.ReasonCode);

        var ok = await f.Temporary.CloseAsync(e.Id, new CloseTemporaryInstructionInput("IncorporateIntoPermanent", "PERM-SOP-1", null), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Equal(nameof(TemporaryInstructionStatus.Incorporated), ok.Data!.TemporaryInstructionStatus);
    }

    [Fact]
    public async Task Close_temporary_instruction_requires_exactly_one_valid_expiry_action()
    {
        var f = Fixture();
        var e = await StartedTemporary(f);

        var r = await f.Temporary.CloseAsync(e.Id, new CloseTemporaryInstructionInput("NotAnAction", "REF", null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.ExpiryActionRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Replace_with_new_temporary_requires_replacement_entry()
    {
        var f = Fixture();
        var e = await StartedTemporary(f);

        var missing = await f.Temporary.CloseAsync(e.Id, new CloseTemporaryInstructionInput("ReplaceWithNewTemporary", null, null), Corr, CancellationToken.None);
        Assert.False(missing.IsSuccessful);
        Assert.Equal(SuspensionReasonCodes.ReplacementRequired, missing.ReasonCode);

        var replacement = SeedTemporary(f);
        var ok = await f.Temporary.CloseAsync(e.Id, new CloseTemporaryInstructionInput("ReplaceWithNewTemporary", null, replacement.Id), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Equal(nameof(TemporaryInstructionStatus.ReplacedByNewTemporary), ok.Data!.TemporaryInstructionStatus);
        Assert.Equal(replacement.Id, ok.Data.ReplacementRegisterEntryId);
    }

    [Fact]
    public async Task Suspend_no_replacement_creates_or_links_suspension_case()
    {
        var f = Fixture();
        var e = await StartedTemporary(f);

        var r = await f.Temporary.CloseAsync(e.Id, new CloseTemporaryInstructionInput("SuspendNoReplacement", null, null), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(nameof(TemporaryInstructionStatus.Suspended), r.Data!.TemporaryInstructionStatus);
        Assert.NotNull(r.Data.SuspensionCaseId);
        Assert.Single(f.Cases.Items);
    }

    // ── isolation / durability ────────────────────────────────────────────────

    [Fact]
    public async Task Cross_tenant_suspension_is_blocked()
    {
        var f = Fixture();
        var foreign = SeedEntry(f, tenantId: OtherTenantId);

        var r = await f.Suspension.OpenAsync(foreign.Id, Open("QualityRisk", "risk"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_retirement_is_blocked()
    {
        var f = Fixture();
        var foreign = SeedEntry(f, tenantId: OtherTenantId);

        var r = await f.Retirement.RequestAsync(foreign.Id, Request(), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Cases_and_controls_are_never_hard_deleted()
    {
        var f = Fixture();
        var (e, caseId) = await Approved(f);
        await f.Suspension.ExecuteAsync(e.Id, caseId, Execute(), Corr, CancellationToken.None);
        await f.Suspension.CloseAsync(e.Id, caseId, new CloseSuspensionCaseInput("DEV-1", null, "PLAN-1"), Corr, CancellationToken.None);

        Assert.DoesNotContain(f.Cases.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Retirements.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Controls.Items, x => x.IsDeleted);
        Assert.NotEmpty(f.Cases.Items);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static OpenSuspensionCaseInput Open(string trigger, string description, Guid? escalationId = null) =>
        new(trigger, description, escalationId);

    private static ApproveSuspensionInput Approve(string role) => new("Suspend", "continued use creates risk", role, "COMM-PLAN-1");

    private static ExecuteSuspensionInput Execute() => new("NOTICE-1", "ACCESS-REMOVED-1", "AFFECTED-BATCHES-1");

    private static RequestRetirementInput Request() => new("superseded by new process", "JUST-1", "TRANS-1", null, null);

    private async Task<(DocumentMasterRegisterEntry Entry, Guid CaseId)> Opened(Harness f)
    {
        var e = SeedEntry(f);
        var c = await f.Suspension.OpenAsync(e.Id, Open("QualityRisk", "contamination risk"), Corr, CancellationToken.None);
        return (e, c.Data!.Id);
    }

    private async Task<(DocumentMasterRegisterEntry Entry, Guid CaseId)> Approved(Harness f)
    {
        var (e, caseId) = await Opened(f);
        await f.Suspension.ApproveAsync(e.Id, caseId, Approve("GQD"), Corr, CancellationToken.None);
        return (e, caseId);
    }

    private async Task<(DocumentMasterRegisterEntry Entry, Guid CaseId)> RetirementRequested(Harness f)
    {
        var e = SeedEntry(f);
        var c = await f.Retirement.RequestAsync(e.Id, Request(), Corr, CancellationToken.None);
        return (e, c.Data!.Id);
    }

    private async Task<(DocumentMasterRegisterEntry Entry, Guid CaseId)> RetirementApproved(Harness f)
    {
        var (e, caseId) = await RetirementRequested(f);
        await f.Retirement.ApproveAsync(e.Id, caseId, new ApproveRetirementInput("GQD"), Corr, CancellationToken.None);
        return (e, caseId);
    }

    private async Task<DocumentMasterRegisterEntry> StartedTemporary(Harness f)
    {
        var e = SeedTemporary(f);
        await f.Temporary.StartAsync(e.Id, new StartTemporaryInstructionInput(null, DateTimeOffset.UtcNow.AddDays(20)), Corr, CancellationToken.None);
        return e;
    }

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var register = new FakeRegisterRepo(tenant);
        var cases = new FakeSuspensionCaseRepo(tenant);
        var retirements = new FakeRetirementCaseRepo(tenant);
        var controls = new FakeTemporaryControlRepo(tenant);
        var escalations = new FakeEscalationRepo(tenant);
        var user = new FakeUser();

        // The REAL FU08 lifecycle engine — FU13 never duplicates the transition matrix.
        var lifecycle = new DocumentLifecycleService(register, new FakeTransitionRepo(tenant), tenant, user,
            Options.Create(new DocumentLifecycleOptions()));

        var suspension = new DocumentSuspensionService(register, cases, escalations, lifecycle, tenant, user);
        var retirement = new DocumentRetirementService(register, retirements, cases, lifecycle, tenant, user);
        var temporary = new TemporaryInstructionService(register, controls, suspension, tenant, user,
            Options.Create(new DocumentWithdrawalOptions()));

        return new Harness(suspension, retirement, temporary, register, cases, retirements, controls, escalations);
    }

    private static DocumentMasterRegisterEntry SeedEntry(
        Harness f, ControlledDocumentLifecycleStatus status = ControlledDocumentLifecycleStatus.Effective, Guid? tenantId = null)
    {
        var e = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? TenantId,
            DocumentTitle = "Document Control",
            DocumentClass = ControlledDocumentClass.Sop,
            DocumentType = DocumentType.Sop,
            Criticality = DocumentCriticality.Critical,
            IsControlledDocument = true,
            LifecycleStatus = status,
            RegisterStatus = DocumentRegisterStatus.Active,
            PermanentUid = "UID-0000001",
            DocumentCode = "GMG-QMS-SOP-0001"
        };
        f.Register.Items.Add(e);
        return e;
    }

    private static DocumentMasterRegisterEntry SeedTemporary(Harness f)
    {
        var e = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            DocumentTitle = "Urgent Instruction",
            DocumentClass = ControlledDocumentClass.UrgentTemporaryInstruction,
            DocumentType = DocumentType.Other,
            Criticality = DocumentCriticality.UrgentTemporary,
            IsControlledDocument = true,
            LifecycleStatus = ControlledDocumentLifecycleStatus.Effective,
            RegisterStatus = DocumentRegisterStatus.Active,
            PermanentUid = "UID-0000009",
            DocumentCode = "GMG-QMS-TMP-0001"
        };
        f.Register.Items.Add(e);
        return e;
    }

    private static DocumentPeriodicReviewEscalation SeedEscalation(Harness f, Guid entryId)
    {
        var esc = new DocumentPeriodicReviewEscalation
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RegisterEntryId = entryId,
            PeriodicReviewId = Guid.NewGuid(),
            EscalationType = ReviewEscalationType.GqdDeterminationRequired,
            Severity = ReviewEscalationSeverity.Critical,
            RequiredRole = ReviewEscalationRole.GQD,
            Description = "overdue critical review"
        };
        f.Escalations.Items.Add(esc);
        return esc;
    }

    private sealed record Harness(
        DocumentSuspensionService Suspension, DocumentRetirementService Retirement, TemporaryInstructionService Temporary,
        FakeRegisterRepo Register, FakeSuspensionCaseRepo Cases, FakeRetirementCaseRepo Retirements,
        FakeTemporaryControlRepo Controls, FakeEscalationRepo Escalations);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444444");
        public string? Email => "fu13@example.test";
        public string? DisplayName => "FU13 Tester";
        public string ActorName => "fu13@example.test";
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

    private sealed class FakeSuspensionCaseRepo(ITenantContext tenant) : IDocumentSuspensionCaseRepository
    {
        public List<DocumentSuspensionCase> Items { get; } = [];
        private IEnumerable<DocumentSuspensionCase> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentSuspensionCase> CreateAsync(DocumentSuspensionCase c, CancellationToken ct = default) { Items.Add(c); return Task.FromResult(c); }
        public Task<DocumentSuspensionCase?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentSuspensionCase>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSuspensionCase>>(Scoped.Where(x => x.RegisterEntryId == entryId).ToList());
        public Task<DocumentSuspensionCase?> GetOpenAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult(Scoped.Where(x => x.RegisterEntryId == entryId
                && x.CaseStatus != SuspensionCaseStatus.Closed && x.CaseStatus != SuspensionCaseStatus.Cancelled && x.CaseStatus != SuspensionCaseStatus.Rejected)
                .OrderByDescending(x => x.CaseNumber).FirstOrDefault());
        public Task<bool> UpdateAsync(DocumentSuspensionCase c, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == c.Id); if (i >= 0) Items[i] = c; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeRetirementCaseRepo(ITenantContext tenant) : IDocumentRetirementCaseRepository
    {
        public List<DocumentRetirementCase> Items { get; } = [];
        private IEnumerable<DocumentRetirementCase> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentRetirementCase> CreateAsync(DocumentRetirementCase c, CancellationToken ct = default) { Items.Add(c); return Task.FromResult(c); }
        public Task<DocumentRetirementCase?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentRetirementCase>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRetirementCase>>(Scoped.Where(x => x.RegisterEntryId == entryId).ToList());
        public Task<bool> UpdateAsync(DocumentRetirementCase c, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == c.Id); if (i >= 0) Items[i] = c; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeTemporaryControlRepo(ITenantContext tenant) : ITemporaryInstructionControlRepository
    {
        public List<TemporaryInstructionControl> Items { get; } = [];
        private IEnumerable<TemporaryInstructionControl> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<TemporaryInstructionControl> CreateAsync(TemporaryInstructionControl c, CancellationToken ct = default) { Items.Add(c); return Task.FromResult(c); }
        public Task<TemporaryInstructionControl?> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.RegisterEntryId == entryId));
        public Task<IReadOnlyList<TemporaryInstructionControl>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemporaryInstructionControl>>(Scoped.OrderBy(x => x.ValidUntil).ToList());
        public Task<bool> UpdateAsync(TemporaryInstructionControl c, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == c.Id); if (i >= 0) Items[i] = c; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeEscalationRepo(ITenantContext tenant) : IDocumentPeriodicReviewEscalationRepository
    {
        public List<DocumentPeriodicReviewEscalation> Items { get; } = [];
        private IEnumerable<DocumentPeriodicReviewEscalation> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentPeriodicReviewEscalation> CreateAsync(DocumentPeriodicReviewEscalation e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<IReadOnlyList<DocumentPeriodicReviewEscalation>> GetByReviewAsync(Guid reviewId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentPeriodicReviewEscalation>>(Scoped.Where(x => x.PeriodicReviewId == reviewId).ToList());
        public Task<IReadOnlyList<DocumentPeriodicReviewEscalation>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentPeriodicReviewEscalation>>(Scoped.Where(x => x.RegisterEntryId == entryId).ToList());
    }

    private sealed class FakeTransitionRepo(ITenantContext tenant) : IDocumentLifecycleTransitionRecordRepository
    {
        public List<DocumentLifecycleTransitionRecord> Items { get; } = [];
        public Task<DocumentLifecycleTransitionRecord> CreateAsync(DocumentLifecycleTransitionRecord r, CancellationToken ct = default) { Items.Add(r); return Task.FromResult(r); }
        public Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLifecycleTransitionRecord>>(Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == entryId).ToList());
        public Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLifecycleTransitionRecord>>(Items.Where(x => x.TenantId == tenant.TenantId).ToList());
    }
}
