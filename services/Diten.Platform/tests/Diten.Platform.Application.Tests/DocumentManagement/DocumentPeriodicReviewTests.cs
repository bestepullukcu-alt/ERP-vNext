using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementPeriodicReview;
using Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU12 — periodic review / extension / overdue tests. Tenant-aware in-memory fakes exercise cycle defaults,
/// due-date maths, the 60-day initiation window, the single-extension rule, GQD approval for Critical, overdue
/// detection and escalation. Lifecycle is never auto-transitioned.
/// </summary>
public sealed class DocumentPeriodicReviewTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private const string Corr = "fu12-corr-1";

    // ── cycle defaults / due date maths ───────────────────────────────────────

    [Theory]
    [InlineData(DocumentCriticality.Critical, 24)]
    [InlineData(DocumentCriticality.Major, 36)]
    [InlineData(DocumentCriticality.Minor, 48)]
    public void Default_review_cycle_matches_criticality(DocumentCriticality criticality, int expectedMonths)
    {
        Assert.Equal(expectedMonths, DocumentReviewCycleCalculator.DefaultCycleMonths(criticality));
    }

    [Fact]
    public async Task Next_review_due_date_calculated_from_effective_date()
    {
        var f = Fixture();
        var effective = DateTimeOffset.UtcNow.AddMonths(-1);
        var e = SeedEntry(f, DocumentCriticality.Critical, effectiveDate: effective);

        var schedule = await f.Service.GetScheduleAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal(24, schedule.Data!.ReviewCycleMonths);
        Assert.Equal(effective.AddMonths(24), schedule.Data.NextReviewDueDate);
    }

    [Fact]
    public async Task Initiation_window_starts_60_days_before_due()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Major, dueDate: DateTimeOffset.UtcNow.AddDays(30));

        var schedule = await f.Service.GetScheduleAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal(schedule.Data!.NextReviewDueDate!.Value.AddDays(-60), schedule.Data.InitiationWindowStartDate);
        Assert.True(schedule.Data.IsDueSoon);
    }

    // ── initiation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Initiate_review_creates_review_when_due_window_open()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, dueDate: DateTimeOffset.UtcNow.AddDays(30));

        var r = await f.Service.InitiateAsync(e.Id, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Initiated", r.Data!.ReviewStatus);
        Assert.Equal(1, r.Data.ReviewNumber);
    }

    [Fact]
    public async Task Initiate_review_is_idempotent_when_open_review_exists()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, dueDate: DateTimeOffset.UtcNow.AddDays(30));
        var first = await f.Service.InitiateAsync(e.Id, Corr, CancellationToken.None);

        var second = await f.Service.InitiateAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal(first.Data!.Id, second.Data!.Id);
        Assert.Single(f.Reviews.Items);
    }

    [Fact]
    public async Task Superseded_document_is_not_scheduled_for_periodic_review()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, dueDate: DateTimeOffset.UtcNow.AddDays(30));
        e.LifecycleStatus = ControlledDocumentLifecycleStatus.Superseded;

        var r = await f.Service.InitiateAsync(e.Id, Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(PeriodicReviewReasonCodes.NotScheduledForReview, r.ReasonCode);
    }

    // ── completion ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Complete_review_requires_evidence()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major);

        var r = await f.Service.CompleteAsync(e.Id, reviewId, new CompletePeriodicReviewInput("ContinueEffective", "", null, null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(PeriodicReviewReasonCodes.EvidenceRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Complete_critical_review_requires_impact_assessment()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Critical);

        var r = await f.Service.CompleteAsync(e.Id, reviewId, new CompletePeriodicReviewInput("ContinueEffective", "REV-1", null, null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(PeriodicReviewReasonCodes.ImpactAssessmentRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Complete_review_updates_last_review_and_next_due_date()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major);

        var r = await f.Service.CompleteAsync(e.Id, reviewId, new CompletePeriodicReviewInput("ContinueEffective", "REV-1", null, null), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        var after = f.Register.Items.Single(x => x.Id == e.Id);
        Assert.NotNull(after.LastPeriodicReviewDate);
        // Major → 36 months from completion.
        Assert.Equal(after.LastPeriodicReviewDate!.Value.AddMonths(36).Date, after.NextReviewDueDate!.Value.Date);
    }

    [Fact]
    public async Task ContinueEffective_decision_does_not_change_lifecycle()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major);
        var before = e.LifecycleStatus;

        await f.Service.CompleteAsync(e.Id, reviewId, new CompletePeriodicReviewInput("ContinueEffective", "REV-1", null, null), Corr, CancellationToken.None);

        Assert.Equal(before, f.Register.Items.Single(x => x.Id == e.Id).LifecycleStatus);
    }

    [Fact]
    public async Task Revise_decision_does_not_auto_transition_lifecycle()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major);

        await f.Service.CompleteAsync(e.Id, reviewId, new CompletePeriodicReviewInput("Revise", "REV-1", "IMP-1", null), Corr, CancellationToken.None);

        // FU08 owns transitions; FU12 only records the decision (AutoTransitionOnReviseDecision is off by default).
        Assert.Equal(ControlledDocumentLifecycleStatus.Effective, f.Register.Items.Single(x => x.Id == e.Id).LifecycleStatus);
    }

    [Fact]
    public async Task Suspend_decision_raises_gqd_determination_escalation()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major);

        await f.Service.CompleteAsync(e.Id, reviewId, new CompletePeriodicReviewInput("Suspend", "REV-1", "IMP-1", null), Corr, CancellationToken.None);

        Assert.Contains(f.Escalations.Items, x => x.EscalationType == ReviewEscalationType.GqdDeterminationRequired);
        // The suspension itself is FU13 — lifecycle untouched here.
        Assert.Equal(ControlledDocumentLifecycleStatus.Effective, f.Register.Items.Single(x => x.Id == e.Id).LifecycleStatus);
    }

    // ── extension ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Request_extension_requires_risk_assessment()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major);

        var r = await f.Service.RequestExtensionAsync(e.Id, reviewId, new RequestPeriodicReviewExtensionInput(30, "", null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(PeriodicReviewReasonCodes.RiskAssessmentRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Extension_must_be_requested_before_due_date()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major, dueDate: DateTimeOffset.UtcNow.AddDays(-1));

        var r = await f.Service.RequestExtensionAsync(e.Id, reviewId, new RequestPeriodicReviewExtensionInput(30, "RISK-1", null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(PeriodicReviewReasonCodes.ReviewAlreadyOverdue, r.ReasonCode);
    }

    [Fact]
    public async Task Extension_max_60_days()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major);

        var r = await f.Service.RequestExtensionAsync(e.Id, reviewId, new RequestPeriodicReviewExtensionInput(61, "RISK-1", null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(PeriodicReviewReasonCodes.ExtensionTooLong, r.ReasonCode);
    }

    [Fact]
    public async Task Only_one_extension_allowed()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major);
        var first = await f.Service.RequestExtensionAsync(e.Id, reviewId, new RequestPeriodicReviewExtensionInput(30, "RISK-1", null), Corr, CancellationToken.None);
        await f.Service.ApproveExtensionAsync(e.Id, reviewId, first.Data!.Id, new ApprovePeriodicReviewExtensionInput("QADocumentation", false, null), Corr, CancellationToken.None);

        var second = await f.Service.RequestExtensionAsync(e.Id, reviewId, new RequestPeriodicReviewExtensionInput(10, "RISK-2", null), Corr, CancellationToken.None);

        Assert.False(second.IsSuccessful);
        Assert.Equal(PeriodicReviewReasonCodes.ExtensionAlreadyUsed, second.ReasonCode);
    }

    [Fact]
    public async Task Critical_extension_requires_GQD_approval()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Critical);
        var ext = await f.Service.RequestExtensionAsync(e.Id, reviewId, new RequestPeriodicReviewExtensionInput(30, "RISK-1", null), Corr, CancellationToken.None);

        var r = await f.Service.ApproveExtensionAsync(e.Id, reviewId, ext.Data!.Id, new ApprovePeriodicReviewExtensionInput("QADocumentation", false, null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(PeriodicReviewReasonCodes.GqdApprovalRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Critical_extension_approval_creates_management_review_escalation()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Critical);
        var ext = await f.Service.RequestExtensionAsync(e.Id, reviewId, new RequestPeriodicReviewExtensionInput(30, "RISK-1", null), Corr, CancellationToken.None);

        var r = await f.Service.ApproveExtensionAsync(e.Id, reviewId, ext.Data!.Id, new ApprovePeriodicReviewExtensionInput("GQD", false, null), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.True(r.Data!.ManagementReviewEscalated);
        Assert.Contains(f.Escalations.Items, x => x.EscalationType == ReviewEscalationType.ManagementReview);
    }

    [Fact]
    public async Task Noncritical_extension_can_be_QA_approved_and_moves_due_date()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major);
        var ext = await f.Service.RequestExtensionAsync(e.Id, reviewId, new RequestPeriodicReviewExtensionInput(30, "RISK-1", null), Corr, CancellationToken.None);

        var r = await f.Service.ApproveExtensionAsync(e.Id, reviewId, ext.Data!.Id, new ApprovePeriodicReviewExtensionInput("QADocumentation", false, null), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        var review = f.Reviews.Items.Single(x => x.Id == reviewId);
        Assert.Equal(PeriodicReviewStatus.Extended, review.ReviewStatus);
        Assert.Equal(ext.Data.ExtendedDueDate, review.ReviewDueDate);
    }

    [Fact]
    public async Task Extension_approved_after_due_date_is_blocked()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major, dueDate: DateTimeOffset.UtcNow.AddSeconds(2));
        var ext = await f.Service.RequestExtensionAsync(e.Id, reviewId, new RequestPeriodicReviewExtensionInput(30, "RISK-1", null), Corr, CancellationToken.None);
        // Simulate the due date having passed before approval.
        f.Extensions.Items.Single(x => x.Id == ext.Data!.Id).OriginalDueDate = DateTimeOffset.UtcNow.AddDays(-1);

        var r = await f.Service.ApproveExtensionAsync(e.Id, reviewId, ext.Data!.Id, new ApprovePeriodicReviewExtensionInput("QADocumentation", false, null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(PeriodicReviewReasonCodes.ReviewAlreadyOverdue, r.ReasonCode);
    }

    [Fact]
    public async Task Reject_extension_requires_reason()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major);
        var ext = await f.Service.RequestExtensionAsync(e.Id, reviewId, new RequestPeriodicReviewExtensionInput(30, "RISK-1", null), Corr, CancellationToken.None);

        var r = await f.Service.RejectExtensionAsync(e.Id, reviewId, ext.Data!.Id, new RejectPeriodicReviewExtensionInput(""), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(PeriodicReviewReasonCodes.ReasonRequired, r.ReasonCode);
    }

    // ── overdue ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Overdue_created_when_due_date_passed_without_completion()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major, dueDate: DateTimeOffset.UtcNow.AddDays(-1));

        var r = await f.Service.EvaluateOverdueAsync(e.Id, Corr, CancellationToken.None);

        Assert.True(r.Data!.IsOverdue);
        Assert.Equal(PeriodicReviewStatus.Overdue, f.Reviews.Items.Single(x => x.Id == reviewId).ReviewStatus);
    }

    [Fact]
    public async Task Critical_overdue_creates_GQD_escalation_with_no_tolerance()
    {
        var f = Fixture();
        var (e, _) = await Initiated(f, DocumentCriticality.Critical, dueDate: DateTimeOffset.UtcNow.AddDays(-1));

        var r = await f.Service.EvaluateOverdueAsync(e.Id, Corr, CancellationToken.None);

        Assert.True(r.Data!.RequiresGqdEscalation);
        var esc = f.Escalations.Items.Single(x => x.EscalationType == ReviewEscalationType.OverdueCritical);
        Assert.Equal(ReviewEscalationSeverity.Critical, esc.Severity);
        Assert.Equal(ReviewEscalationRole.GQD, esc.RequiredRole);
    }

    [Fact]
    public async Task Extension_expired_without_completion_creates_overdue_and_escalation()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Critical);
        var ext = await f.Service.RequestExtensionAsync(e.Id, reviewId, new RequestPeriodicReviewExtensionInput(30, "RISK-1", null), Corr, CancellationToken.None);
        await f.Service.ApproveExtensionAsync(e.Id, reviewId, ext.Data!.Id, new ApprovePeriodicReviewExtensionInput("GQD", false, null), Corr, CancellationToken.None);
        // Simulate the extended due date having passed.
        var stored = f.Extensions.Items.Single(x => x.Id == ext.Data.Id);
        stored.ExtendedDueDate = DateTimeOffset.UtcNow.AddDays(-1);
        f.Reviews.Items.Single(x => x.Id == reviewId).ReviewDueDate = DateTimeOffset.UtcNow.AddDays(-1);

        var r = await f.Service.EvaluateOverdueAsync(e.Id, Corr, CancellationToken.None);

        Assert.True(r.Data!.IsOverdue);
        Assert.Equal(PeriodicReviewExtensionStatus.Expired, f.Extensions.Items.Single(x => x.Id == ext.Data.Id).Status);
        Assert.Contains(f.Escalations.Items, x => x.EscalationType == ReviewEscalationType.ExtensionExpired && x.RequiredRole == ReviewEscalationRole.GQD);
    }

    [Fact]
    public async Task No_second_extension_after_expiry()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Major);
        var ext = await f.Service.RequestExtensionAsync(e.Id, reviewId, new RequestPeriodicReviewExtensionInput(30, "RISK-1", null), Corr, CancellationToken.None);
        await f.Service.ApproveExtensionAsync(e.Id, reviewId, ext.Data!.Id, new ApprovePeriodicReviewExtensionInput("QADocumentation", false, null), Corr, CancellationToken.None);
        f.Extensions.Items.Single(x => x.Id == ext.Data.Id).Status = PeriodicReviewExtensionStatus.Expired;

        var second = await f.Service.RequestExtensionAsync(e.Id, reviewId, new RequestPeriodicReviewExtensionInput(10, "RISK-2", null), Corr, CancellationToken.None);

        Assert.False(second.IsSuccessful);
        Assert.Equal(PeriodicReviewReasonCodes.ExtensionAlreadyUsed, second.ReasonCode);
    }

    [Fact]
    public async Task Overdue_evaluation_is_idempotent_and_does_not_duplicate_escalations()
    {
        var f = Fixture();
        var (e, _) = await Initiated(f, DocumentCriticality.Critical, dueDate: DateTimeOffset.UtcNow.AddDays(-1));

        await f.Service.EvaluateOverdueAsync(e.Id, Corr, CancellationToken.None);
        await f.Service.EvaluateOverdueAsync(e.Id, Corr, CancellationToken.None);

        Assert.Single(f.Escalations.Items.Where(x => x.EscalationType == ReviewEscalationType.OverdueCritical));
    }

    [Fact]
    public async Task Overdue_does_not_automatically_suspend_the_document()
    {
        var f = Fixture();
        var (e, _) = await Initiated(f, DocumentCriticality.Critical, dueDate: DateTimeOffset.UtcNow.AddDays(-1));

        await f.Service.EvaluateOverdueAsync(e.Id, Corr, CancellationToken.None);

        // Suspension is FU13; FU12 only escalates.
        Assert.Equal(ControlledDocumentLifecycleStatus.Effective, f.Register.Items.Single(x => x.Id == e.Id).LifecycleStatus);
    }

    // ── isolation / durability ────────────────────────────────────────────────

    [Fact]
    public async Task Cross_tenant_review_is_blocked()
    {
        var f = Fixture();
        var foreign = SeedEntry(f, DocumentCriticality.Critical, dueDate: DateTimeOffset.UtcNow.AddDays(30), tenantId: OtherTenantId);

        var r = await f.Service.InitiateAsync(foreign.Id, Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Review_extension_and_escalation_are_never_hard_deleted()
    {
        var f = Fixture();
        var (e, reviewId) = await Initiated(f, DocumentCriticality.Critical, dueDate: DateTimeOffset.UtcNow.AddDays(-1));
        await f.Service.EvaluateOverdueAsync(e.Id, Corr, CancellationToken.None);

        Assert.DoesNotContain(f.Reviews.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Extensions.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Escalations.Items, x => x.IsDeleted);
        Assert.NotEmpty(f.Reviews.Items);
    }

    // ── fixtures ──────────────────────────────────────────────────────────────

    private async Task<(DocumentMasterRegisterEntry Entry, Guid ReviewId)> Initiated(
        Harness f, DocumentCriticality criticality, DateTimeOffset? dueDate = null)
    {
        var e = SeedEntry(f, criticality, dueDate: dueDate ?? DateTimeOffset.UtcNow.AddDays(30));
        var review = await f.Service.InitiateAsync(e.Id, Corr, CancellationToken.None);
        return (e, review.Data!.Id);
    }

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var register = new FakeRegisterRepo(tenant);
        var reviews = new FakeReviewRepo(tenant);
        var extensions = new FakeExtensionRepo(tenant);
        var escalations = new FakeEscalationRepo(tenant);
        var service = new DocumentPeriodicReviewService(register, reviews, extensions, escalations,
            new DocumentPeriodicReviewStatusEvaluator(), tenant, new FakeUser(), Options.Create(new DocumentPeriodicReviewOptions()));
        return new Harness(service, register, reviews, extensions, escalations);
    }

    private static DocumentMasterRegisterEntry SeedEntry(
        Harness f, DocumentCriticality criticality, DateTimeOffset? effectiveDate = null, DateTimeOffset? dueDate = null, Guid? tenantId = null)
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
            LifecycleStatus = ControlledDocumentLifecycleStatus.Effective,
            RegisterStatus = DocumentRegisterStatus.Active,
            EffectiveDate = effectiveDate ?? DateTimeOffset.UtcNow.AddMonths(-12),
            NextReviewDueDate = dueDate
        };
        f.Register.Items.Add(e);
        return e;
    }

    private sealed record Harness(
        DocumentPeriodicReviewService Service, FakeRegisterRepo Register, FakeReviewRepo Reviews,
        FakeExtensionRepo Extensions, FakeEscalationRepo Escalations);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444444");
        public string? Email => "fu12@example.test";
        public string? DisplayName => "FU12 Tester";
        public string ActorName => "fu12@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeRegisterRepo(ITenantContext tenant) : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];
        private IEnumerable<DocumentMasterRegisterEntry> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) { Items.Add(entry); return Task.FromResult(entry); }
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string uid, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.PermanentUid == uid));
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.DocumentCode == code));
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.ControlledDocumentId == id));
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == entry.Id); if (i >= 0) Items[i] = entry; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeReviewRepo(ITenantContext tenant) : IDocumentPeriodicReviewRepository
    {
        public List<DocumentPeriodicReview> Items { get; } = [];
        private IEnumerable<DocumentPeriodicReview> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentPeriodicReview> CreateAsync(DocumentPeriodicReview review, CancellationToken ct = default) { Items.Add(review); return Task.FromResult(review); }
        public Task<DocumentPeriodicReview?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentPeriodicReview>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentPeriodicReview>>(Scoped.Where(x => x.RegisterEntryId == entryId).ToList());
        public Task<DocumentPeriodicReview?> GetOpenAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult(Scoped.Where(x => x.RegisterEntryId == entryId
                    && x.ReviewStatus != PeriodicReviewStatus.Completed && x.ReviewStatus != PeriodicReviewStatus.Cancelled)
                .OrderByDescending(x => x.ReviewNumber).FirstOrDefault());
        public Task<bool> UpdateAsync(DocumentPeriodicReview review, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == review.Id); if (i >= 0) Items[i] = review; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeExtensionRepo(ITenantContext tenant) : IDocumentPeriodicReviewExtensionRepository
    {
        public List<DocumentPeriodicReviewExtension> Items { get; } = [];
        private IEnumerable<DocumentPeriodicReviewExtension> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentPeriodicReviewExtension> CreateAsync(DocumentPeriodicReviewExtension e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentPeriodicReviewExtension?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentPeriodicReviewExtension>> GetByReviewAsync(Guid reviewId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentPeriodicReviewExtension>>(Scoped.Where(x => x.PeriodicReviewId == reviewId).ToList());
        public Task<bool> UpdateAsync(DocumentPeriodicReviewExtension e, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == e.Id); if (i >= 0) Items[i] = e; return Task.FromResult(i >= 0); }
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
}
