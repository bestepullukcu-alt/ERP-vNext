using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementExternalDocuments;
using Diten.Platform.Application.Features.DocumentManagementExternalDocuments.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU14 — External Document Register tests (GMG-QMS-SOP-0001 §10). Tenant-aware in-memory fakes exercise
/// registration validation, the monitoring cadence and evidence trail, the 10-working-day regulated impact
/// deadline, internal register linking, and the boundaries that keep an external document from behaving like an
/// internal controlled document.
/// </summary>
public sealed class ExternalDocumentRegisterTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid Owner = Guid.Parse("d0000000-0000-0000-0000-000000000014");
    private const string Corr = "fu14-corr-1";

    // ── registration + validation ─────────────────────────────────────────────

    [Fact]
    public async Task Create_external_document_register_entry()
    {
        var f = Fixture();

        var r = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal("Active", r.Data!.ExternalDocumentStatus);
        Assert.Equal("CurrentEffective", r.Data.SourceStatus);
        Assert.Equal("Guideline", r.Data.ExternalDocumentType);
        // The boundary is stated on every read: this is not an internal controlled document.
        Assert.Contains("never edited or versioned", r.Data.BoundaryStatement);
    }

    [Fact]
    public async Task Create_requires_title_source_owner_and_frequency()
    {
        var f = Fixture();

        var noTitle = await f.Service.CreateAsync(Guideline() with { ExternalDocumentTitle = "  " }, Corr, CancellationToken.None);
        var noAuthority = await f.Service.CreateAsync(Guideline() with { ExternalAuthorityName = "" }, Corr, CancellationToken.None);
        var noSource = await f.Service.CreateAsync(Guideline() with { SourceReference = "" }, Corr, CancellationToken.None);
        var noOwner = await f.Service.CreateAsync(Guideline() with { MonitoringOwnerUserId = null, MonitoringOwnerRole = null }, Corr, CancellationToken.None);
        var noFrequency = await f.Service.CreateAsync(Guideline() with { MonitoringFrequency = "Fortnightly" }, Corr, CancellationToken.None);
        var noStatus = await f.Service.CreateAsync(Guideline() with { SourceStatus = null }, Corr, CancellationToken.None);

        Assert.Equal(ExternalDocumentReasonCodes.TitleRequired, noTitle.ReasonCode);
        Assert.Equal(ExternalDocumentReasonCodes.AuthorityRequired, noAuthority.ReasonCode);
        Assert.Equal(ExternalDocumentReasonCodes.SourceReferenceRequired, noSource.ReasonCode);
        Assert.Equal(ExternalDocumentReasonCodes.MonitoringOwnerRequired, noOwner.ReasonCode);
        Assert.Equal(ExternalDocumentReasonCodes.MonitoringFrequencyRequired, noFrequency.ReasonCode);
        Assert.Equal(ExternalDocumentReasonCodes.SourceStatusRequired, noStatus.ReasonCode);
        Assert.Empty(f.ExternalDocuments.Items);
    }

    [Fact]
    public async Task Next_check_due_date_is_calculated_from_frequency()
    {
        var f = Fixture();
        var before = DateTimeOffset.UtcNow;

        var quarterly = await f.Service.CreateAsync(Guideline() with { MonitoringFrequency = "Quarterly" }, Corr, CancellationToken.None);
        var onTrigger = await f.Service.CreateAsync(Guideline() with { MonitoringFrequency = "OnTrigger" }, Corr, CancellationToken.None);

        Assert.NotNull(quarterly.Data!.NextCheckDueDate);
        Assert.InRange(quarterly.Data.NextCheckDueDate!.Value, before.AddMonths(3).AddMinutes(-5), before.AddMonths(3).AddMinutes(5));
        // OnTrigger is event-driven: it has no schedule, so it can never be "overdue".
        Assert.Null(onTrigger.Data!.NextCheckDueDate);
    }

    [Fact]
    public async Task Draft_consultation_is_regulatory_intelligence_only()
    {
        var f = Fixture();

        var r = await f.Service.CreateAsync(
            Guideline() with { SourceStatus = "DraftConsultation", HasRaImpact = true }, Corr, CancellationToken.None);

        Assert.True(r.Data!.IsRegulatoryIntelligenceOnly);
        Assert.Contains("regulatory intelligence", r.Data.BoundaryStatement);
        // A draft source never becomes a mandatory effective requirement, even with a regulated impact flag.
        Assert.False(r.Data.RequiresImpactAssessment);
        Assert.Equal("NotRequired", r.Data.ImpactAssessmentStatus);
    }

    [Fact]
    public async Task Draft_consultation_cannot_be_promoted_to_effective_without_evidence()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(Guideline() with { SourceStatus = "DraftConsultation" }, Corr, CancellationToken.None);

        var blocked = await f.Service.UpdateAsync(created.Data!.Id,
            Guideline() with { SourceStatus = "CurrentEffective", SourceEffectiveDate = null }, Corr, CancellationToken.None);

        Assert.False(blocked.IsSuccessful);
        Assert.Equal(ExternalDocumentReasonCodes.EffectivePromotionEvidenceRequired, blocked.ReasonCode);

        var allowed = await f.Service.UpdateAsync(created.Data.Id,
            Guideline() with { SourceStatus = "CurrentEffective", SourceEffectiveDate = DateTimeOffset.UtcNow }, Corr, CancellationToken.None);

        Assert.True(allowed.IsSuccessful);
        Assert.Equal("CurrentEffective", allowed.Data!.SourceStatus);
    }

    // ── monitoring ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Record_monitoring_check_requires_evidence_and_source()
    {
        var f = Fixture();
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);

        var noEvidence = await f.Service.RecordMonitoringCheckAsync(entry.Data!.Id,
            Check() with { EvidenceReference = "" }, Corr, CancellationToken.None);
        var noSource = await f.Service.RecordMonitoringCheckAsync(entry.Data.Id,
            Check() with { MonitoringSource = "" }, Corr, CancellationToken.None);

        Assert.Equal(ExternalDocumentReasonCodes.EvidenceReferenceRequired, noEvidence.ReasonCode);
        Assert.Equal(ExternalDocumentReasonCodes.MonitoringSourceRequired, noSource.ReasonCode);
        Assert.Empty(f.Checks.Items);
    }

    [Fact]
    public async Task Change_detected_requires_a_change_summary()
    {
        var f = Fixture();
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);

        var r = await f.Service.RecordMonitoringCheckAsync(entry.Data!.Id,
            Check() with { ChangeDetected = true, ChangeSummary = null }, Corr, CancellationToken.None);

        Assert.Equal(ExternalDocumentReasonCodes.ChangeSummaryRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Record_monitoring_check_updates_last_and_next_check()
    {
        var f = Fixture();
        var entry = await f.Service.CreateAsync(Guideline() with { MonitoringFrequency = "Monthly" }, Corr, CancellationToken.None);
        var checkDate = DateTimeOffset.UtcNow;

        var r = await f.Service.RecordMonitoringCheckAsync(entry.Data!.Id, Check() with { CheckDate = checkDate }, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        var stored = f.ExternalDocuments.Items.Single(x => x.Id == entry.Data.Id);
        Assert.Equal(checkDate, stored.LastCheckedAt);
        Assert.Equal(checkDate.AddMonths(1), stored.NextCheckDueDate);
        Assert.Equal(ExternalDocumentStatus.Monitoring, stored.ExternalDocumentStatus);
        Assert.Single(f.Checks.Items);
    }

    [Fact]
    public async Task Change_detected_creates_pending_impact_assessment()
    {
        var f = Fixture();
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);

        await f.Service.RecordMonitoringCheckAsync(entry.Data!.Id,
            Check() with { ChangeDetected = true, ChangeSummary = "Annex 1 revised" }, Corr, CancellationToken.None);

        var stored = f.ExternalDocuments.Items.Single(x => x.Id == entry.Data.Id);
        Assert.True(stored.RequiresImpactAssessment);
        Assert.Equal(ExternalImpactAssessmentStatus.Pending, stored.ImpactAssessmentStatus);
        Assert.Equal(ExternalDocumentStatus.ActionRequired, stored.ExternalDocumentStatus);

        var assessment = Assert.Single(f.Assessments.Items);
        Assert.Equal(ExternalImpactTriggerType.VersionChange, assessment.TriggerType);
        Assert.Equal(ExternalImpactAssessmentStatus.Pending, assessment.AssessmentStatus);
    }

    [Fact]
    public async Task Monitoring_due_query_returns_overdue_items()
    {
        var f = Fixture();
        var overdue = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);
        var current = await f.Service.CreateAsync(Guideline() with { ExternalDocumentTitle = "Not due yet" }, Corr, CancellationToken.None);

        // Push one entry's due date into the past.
        f.ExternalDocuments.Items.Single(x => x.Id == overdue.Data!.Id).NextCheckDueDate = DateTimeOffset.UtcNow.AddDays(-3);

        var r = await f.Service.GetMonitoringDueAsync(Corr, CancellationToken.None);

        var row = Assert.Single(r.Data!);
        Assert.Equal(overdue.Data!.Id, row.Id);
        Assert.True(row.NeverChecked);
        Assert.Equal(3, row.DaysOverdue);
        Assert.DoesNotContain(r.Data!, x => x.Id == current.Data!.Id);
    }

    [Fact]
    public async Task Archived_external_document_is_not_monitoring_due()
    {
        var f = Fixture();
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);
        f.ExternalDocuments.Items.Single(x => x.Id == entry.Data!.Id).NextCheckDueDate = DateTimeOffset.UtcNow.AddDays(-10);

        await f.Service.ArchiveAsync(entry.Data!.Id, new ArchiveExternalDocumentInput("Superseded by new guideline"), Corr, CancellationToken.None);

        var r = await f.Service.GetMonitoringDueAsync(Corr, CancellationToken.None);
        Assert.Empty(r.Data!);
    }

    // ── supersession / withdrawal ─────────────────────────────────────────────

    [Fact]
    public async Task Mark_superseded_sets_status_and_requires_impact_when_internal_links_exist()
    {
        var f = Fixture();
        var internalEntry = SeedInternalEntry(f);
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);
        await f.Service.LinkToInternalRegisterAsync(entry.Data!.Id,
            new LinkExternalDocumentToInternalInput(internalEntry.Id, "ImplementsRequirement", null), Corr, CancellationToken.None);

        var r = await f.Service.MarkSupersededAsync(entry.Data.Id,
            new MarkExternalDocumentSupersededInput(null, "Replaced by Rev 3"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Superseded", r.Data!.SourceStatus);
        Assert.NotNull(r.Data.SourceSupersededDate);
        // A dependent internal document exists → this is an action, not just a filing update.
        Assert.Equal("ActionRequired", r.Data.ExternalDocumentStatus);
        Assert.True(r.Data.RequiresImpactAssessment);
        Assert.Contains(f.Assessments.Items, a => a.TriggerType == ExternalImpactTriggerType.Supersession);
        Assert.All(f.Links.Items, l => Assert.Equal(ExternalDocumentLinkStatus.ActionRequired, l.LinkStatus));
    }

    [Fact]
    public async Task Mark_superseded_without_internal_links_stays_superseded()
    {
        var f = Fixture();
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);

        var r = await f.Service.MarkSupersededAsync(entry.Data!.Id, new MarkExternalDocumentSupersededInput(null, null), Corr, CancellationToken.None);

        Assert.Equal("Superseded", r.Data!.ExternalDocumentStatus);
        Assert.Empty(f.Assessments.Items);
    }

    [Fact]
    public async Task Withdrawn_external_document_with_internal_links_marks_action_required()
    {
        var f = Fixture();
        var internalEntry = SeedInternalEntry(f);
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);
        await f.Service.LinkToInternalRegisterAsync(entry.Data!.Id,
            new LinkExternalDocumentToInternalInput(internalEntry.Id, "References", null), Corr, CancellationToken.None);

        var r = await f.Service.UpdateAsync(entry.Data.Id, Guideline() with { SourceStatus = "Withdrawn" }, Corr, CancellationToken.None);

        Assert.Equal("ActionRequired", r.Data!.ExternalDocumentStatus);
        Assert.Contains(f.Assessments.Items, a => a.AssessmentStatus == ExternalImpactAssessmentStatus.Pending);
    }

    [Fact]
    public async Task External_document_archive_is_a_soft_status_not_a_delete()
    {
        var f = Fixture();
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);

        var r = await f.Service.ArchiveAsync(entry.Data!.Id, new ArchiveExternalDocumentInput("No longer applicable"), Corr, CancellationToken.None);

        Assert.Equal("Archived", r.Data!.ExternalDocumentStatus);
        Assert.Single(f.ExternalDocuments.Items);
        Assert.DoesNotContain(f.ExternalDocuments.Items, x => x.IsDeleted);

        // An archived source is a closed record: it is no longer editable.
        var edit = await f.Service.UpdateAsync(entry.Data.Id, Guideline(), Corr, CancellationToken.None);
        Assert.Equal(ExternalDocumentReasonCodes.ArchivedNotEditable, edit.ReasonCode);
    }

    // ── impact assessment ─────────────────────────────────────────────────────

    [Fact]
    public async Task Create_impact_assessment_for_RA_impact_is_due_in_10_working_days()
    {
        var f = Fixture();
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);
        var trigger = new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.Zero); // a Monday

        var r = await f.Service.CreateImpactAssessmentAsync(entry.Data!.Id,
            Impact() with { HasRaImpact = true, TriggerDate = trigger }, Corr, CancellationToken.None);

        // Mon 6 Jul + 10 working days = Mon 20 Jul (two weekends skipped).
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero), r.Data!.DueDate);
        Assert.Equal("Pending", r.Data.AssessmentStatus);
    }

    [Fact]
    public async Task Create_impact_assessment_for_PV_impact_is_due_in_10_working_days()
    {
        var f = Fixture();
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);
        var trigger = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero); // a Thursday

        var r = await f.Service.CreateImpactAssessmentAsync(entry.Data!.Id,
            Impact() with { HasPvImpact = true, TriggerDate = trigger }, Corr, CancellationToken.None);

        // Thu 9 Jul + 10 working days = Thu 23 Jul.
        Assert.Equal(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero), r.Data!.DueDate);
    }

    [Fact]
    public async Task Non_regulated_impact_does_not_use_the_10_working_day_clock()
    {
        var f = Fixture();
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);
        var trigger = new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.Zero);

        var r = await f.Service.CreateImpactAssessmentAsync(entry.Data!.Id,
            Impact() with { HasTrainingImpact = true, TriggerDate = trigger }, Corr, CancellationToken.None);

        Assert.Equal(trigger.AddDays(30), r.Data!.DueDate);
    }

    [Fact]
    public async Task Complete_impact_assessment_requires_evidence()
    {
        var f = Fixture();
        var (entry, assessment) = await PendingAssessment(f, Impact() with { HasRaImpact = true });

        var r = await f.Service.CompleteImpactAssessmentAsync(entry, assessment,
            Complete() with { AssessmentEvidenceReference = "  " }, Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(ExternalDocumentReasonCodes.AssessmentEvidenceRequired, r.ReasonCode);
        Assert.Equal(ExternalImpactAssessmentStatus.Pending, f.Assessments.Items.Single().AssessmentStatus);
    }

    [Fact]
    public async Task Complete_document_impact_requires_internal_link_or_action_reference()
    {
        var f = Fixture();
        var (entry, assessment) = await PendingAssessment(f, Impact() with { HasDocumentImpact = true });

        var blocked = await f.Service.CompleteImpactAssessmentAsync(entry, assessment,
            Complete() with { ActionReference = null }, Corr, CancellationToken.None);
        Assert.Equal(ExternalDocumentReasonCodes.DocumentImpactActionRequired, blocked.ReasonCode);

        // An action reference satisfies it...
        var withAction = await f.Service.CompleteImpactAssessmentAsync(entry, assessment,
            Complete() with { ActionReference = "CC-2026-014" }, Corr, CancellationToken.None);
        Assert.True(withAction.IsSuccessful);
    }

    [Fact]
    public async Task Complete_document_impact_is_satisfied_by_an_internal_link()
    {
        var f = Fixture();
        var internalEntry = SeedInternalEntry(f);
        var (entry, assessment) = await PendingAssessment(f, Impact() with { HasDocumentImpact = true });
        await f.Service.LinkToInternalRegisterAsync(entry,
            new LinkExternalDocumentToInternalInput(internalEntry.Id, "ImpactedBy", null), Corr, CancellationToken.None);

        var r = await f.Service.CompleteImpactAssessmentAsync(entry, assessment,
            Complete() with { ActionReference = null }, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
    }

    [Fact]
    public async Task Complete_no_action_assessment_closes_the_pending_status()
    {
        var f = Fixture();
        var (entry, assessment) = await PendingAssessment(f, Impact() with { HasRaImpact = true });

        var r = await f.Service.CompleteImpactAssessmentAsync(entry, assessment,
            Complete() with { RecommendedAction = "NoAction" }, Corr, CancellationToken.None);

        Assert.Equal("Completed", r.Data!.AssessmentStatus);
        Assert.Equal("NoAction", r.Data.RecommendedAction);

        var stored = f.ExternalDocuments.Items.Single(x => x.Id == entry);
        Assert.False(stored.RequiresImpactAssessment);
        Assert.Equal(ExternalImpactAssessmentStatus.Completed, stored.ImpactAssessmentStatus);
        Assert.Null(stored.ImpactAssessmentDueDate);
    }

    [Fact]
    public async Task Completing_twice_is_refused()
    {
        var f = Fixture();
        var (entry, assessment) = await PendingAssessment(f, Impact() with { HasRaImpact = true });
        await f.Service.CompleteImpactAssessmentAsync(entry, assessment, Complete(), Corr, CancellationToken.None);

        var again = await f.Service.CompleteImpactAssessmentAsync(entry, assessment, Complete(), Corr, CancellationToken.None);

        Assert.Equal(ExternalDocumentReasonCodes.AlreadyCompleted, again.ReasonCode);
    }

    [Fact]
    public async Task RecommendedAction_Revise_does_not_auto_transition_the_internal_document()
    {
        var f = Fixture();
        var internalEntry = SeedInternalEntry(f);
        var originalStatus = internalEntry.LifecycleStatus;
        var originalRegisterStatus = internalEntry.RegisterStatus;
        var (entry, assessment) = await PendingAssessment(f, Impact() with { HasRaImpact = true, HasDocumentImpact = true });
        await f.Service.LinkToInternalRegisterAsync(entry,
            new LinkExternalDocumentToInternalInput(internalEntry.Id, "ImplementsRequirement", null), Corr, CancellationToken.None);

        var r = await f.Service.CompleteImpactAssessmentAsync(entry, assessment,
            Complete() with { RecommendedAction = "ReviseInternalDocument", ActionReference = "CC-2026-021" }, Corr, CancellationToken.None);

        Assert.Equal("ReviseInternalDocument", r.Data!.RecommendedAction);
        // The recommendation is recorded; the internal document is untouched (FU08/FU13 remain the only paths).
        var stored = f.InternalRegister.Items.Single(x => x.Id == internalEntry.Id);
        Assert.Equal(originalStatus, stored.LifecycleStatus);
        Assert.Equal(originalRegisterStatus, stored.RegisterStatus);
        Assert.Null(stored.SupersededByRegisterEntryId);
    }

    [Fact]
    public async Task RecommendedAction_Suspend_does_not_suspend_the_internal_document()
    {
        var f = Fixture();
        var internalEntry = SeedInternalEntry(f);
        var (entry, assessment) = await PendingAssessment(f, Impact() with { HasGmpImpact = true });

        await f.Service.CompleteImpactAssessmentAsync(entry, assessment,
            Complete() with { RecommendedAction = "SuspendInternalDocument" }, Corr, CancellationToken.None);

        Assert.Equal(ControlledDocumentLifecycleStatus.Effective, f.InternalRegister.Items.Single(x => x.Id == internalEntry.Id).LifecycleStatus);
    }

    [Fact]
    public async Task Impact_overdue_query_returns_and_persists_overdue_assessments()
    {
        var f = Fixture();
        var (entry, assessmentId) = await PendingAssessment(f, Impact() with { HasRaImpact = true });
        f.Assessments.Items.Single(x => x.Id == assessmentId).DueDate = DateTimeOffset.UtcNow.AddDays(-2);

        var r = await f.Service.GetOverdueImpactAssessmentsAsync(Corr, CancellationToken.None);

        var row = Assert.Single(r.Data!);
        Assert.Equal(assessmentId, row.Id);
        Assert.True(row.IsOverdue);
        Assert.Equal(ExternalImpactAssessmentStatus.Overdue, f.Assessments.Items.Single().AssessmentStatus);

        var stored = f.ExternalDocuments.Items.Single(x => x.Id == entry);
        Assert.Equal(ExternalImpactAssessmentStatus.Overdue, stored.ImpactAssessmentStatus);
        Assert.Equal(ExternalDocumentStatus.ActionRequired, stored.ExternalDocumentStatus);
    }

    [Fact]
    public async Task Completed_assessments_are_not_reported_overdue()
    {
        var f = Fixture();
        var (entry, assessmentId) = await PendingAssessment(f, Impact() with { HasRaImpact = true });
        await f.Service.CompleteImpactAssessmentAsync(entry, assessmentId, Complete(), Corr, CancellationToken.None);
        f.Assessments.Items.Single().DueDate = DateTimeOffset.UtcNow.AddDays(-5);

        var r = await f.Service.GetOverdueImpactAssessmentsAsync(Corr, CancellationToken.None);

        Assert.Empty(r.Data!);
    }

    // ── internal register links ───────────────────────────────────────────────

    [Fact]
    public async Task Link_external_to_internal_register_entry()
    {
        var f = Fixture();
        var internalEntry = SeedInternalEntry(f);
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);

        var r = await f.Service.LinkToInternalRegisterAsync(entry.Data!.Id,
            new LinkExternalDocumentToInternalInput(internalEntry.Id, "ImplementsRequirement", "GMP Ch.4"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(internalEntry.Id, r.Data!.InternalRegisterEntryId);
        Assert.Equal("ImplementsRequirement", r.Data.LinkType);
        Assert.Equal("Active", r.Data.LinkStatus);
    }

    [Fact]
    public async Task Link_is_idempotent()
    {
        var f = Fixture();
        var internalEntry = SeedInternalEntry(f);
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);
        var input = new LinkExternalDocumentToInternalInput(internalEntry.Id, "References", null);

        var first = await f.Service.LinkToInternalRegisterAsync(entry.Data!.Id, input, Corr, CancellationToken.None);
        var second = await f.Service.LinkToInternalRegisterAsync(entry.Data.Id, input, Corr, CancellationToken.None);

        Assert.True(second.IsSuccessful);
        Assert.Equal(first.Data!.Id, second.Data!.Id);
        Assert.Single(f.Links.Items);
    }

    [Fact]
    public async Task Cross_tenant_link_is_blocked()
    {
        var f = Fixture();
        var foreignEntry = SeedInternalEntry(f, tenantId: OtherTenantId);
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);

        var r = await f.Service.LinkToInternalRegisterAsync(entry.Data!.Id,
            new LinkExternalDocumentToInternalInput(foreignEntry.Id, "References", null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(404, r.StatusCode);
        Assert.Equal(ExternalDocumentReasonCodes.InternalEntryNotFound, r.ReasonCode);
        Assert.Empty(f.Links.Items);
    }

    [Fact]
    public async Task Linking_to_an_entry_flagged_as_external_is_refused()
    {
        var f = Fixture();
        var externalFlagged = SeedInternalEntry(f);
        externalFlagged.IsExternalDocument = true;
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);

        var r = await f.Service.LinkToInternalRegisterAsync(entry.Data!.Id,
            new LinkExternalDocumentToInternalInput(externalFlagged.Id, "References", null), Corr, CancellationToken.None);

        Assert.Equal(ExternalDocumentReasonCodes.InternalEntryIsExternal, r.ReasonCode);
    }

    [Fact]
    public async Task Closing_a_link_is_a_status_change_not_a_delete()
    {
        var f = Fixture();
        var internalEntry = SeedInternalEntry(f);
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);
        var link = await f.Service.LinkToInternalRegisterAsync(entry.Data!.Id,
            new LinkExternalDocumentToInternalInput(internalEntry.Id, "References", null), Corr, CancellationToken.None);

        var r = await f.Service.CloseInternalLinkAsync(entry.Data.Id, link.Data!.Id, Corr, CancellationToken.None);

        Assert.Equal("Closed", r.Data!.LinkStatus);
        Assert.Single(f.Links.Items);
        Assert.DoesNotContain(f.Links.Items, x => x.IsDeleted);
    }

    // ── isolation / durability ────────────────────────────────────────────────

    [Fact]
    public async Task Cross_tenant_external_document_is_not_readable()
    {
        var f = Fixture();
        var foreign = new ExternalDocumentRegisterEntry
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, ExternalDocumentTitle = "Foreign guideline",
            ExternalAuthorityName = "Other authority", SourceReference = "OTHER-1"
        };
        f.ExternalDocuments.Items.Add(foreign);

        var r = await f.Service.GetAsync(foreign.Id, Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Nothing_in_the_external_register_is_ever_hard_deleted()
    {
        var f = Fixture();
        var internalEntry = SeedInternalEntry(f);
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);
        await f.Service.LinkToInternalRegisterAsync(entry.Data!.Id,
            new LinkExternalDocumentToInternalInput(internalEntry.Id, "References", null), Corr, CancellationToken.None);
        await f.Service.RecordMonitoringCheckAsync(entry.Data.Id, Check() with { ChangeDetected = true, ChangeSummary = "revised" }, Corr, CancellationToken.None);
        await f.Service.MarkSupersededAsync(entry.Data.Id, new MarkExternalDocumentSupersededInput(null, null), Corr, CancellationToken.None);
        await f.Service.ArchiveAsync(entry.Data.Id, new ArchiveExternalDocumentInput("closed"), Corr, CancellationToken.None);

        Assert.DoesNotContain(f.ExternalDocuments.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Checks.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Assessments.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Links.Items, x => x.IsDeleted);
        Assert.NotEmpty(f.ExternalDocuments.Items);
        Assert.NotEmpty(f.Checks.Items);
    }

    /// <summary>
    /// The register stores REFERENCES only — a source URL, a source reference, an evidence reference. No property
    /// on any FU14 aggregate can carry document content, so no external bytes can reach Mongo.
    /// </summary>
    [Fact]
    public void No_external_document_aggregate_exposes_a_binary_content_property()
    {
        var types = new[]
        {
            typeof(ExternalDocumentRegisterEntry), typeof(ExternalDocumentMonitoringCheck),
            typeof(ExternalDocumentImpactAssessment), typeof(ExternalDocumentInternalLink)
        };

        foreach (var type in types)
        {
            Assert.DoesNotContain(type.GetProperties(), p =>
                p.PropertyType == typeof(byte[]) || p.PropertyType == typeof(Stream) || p.PropertyType == typeof(Memory<byte>));
        }
    }

    [Fact]
    public void Working_day_calculation_skips_weekends()
    {
        // Friday + 1 working day = Monday.
        var friday = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            ExternalDocumentScheduleCalculator.AddWorkingDays(friday, 1));

        // 10 working days never lands on a weekend.
        for (var i = 0; i < 7; i++)
        {
            var due = ExternalDocumentScheduleCalculator.AddWorkingDays(friday.AddDays(i), 10);
            Assert.DoesNotContain(due.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<(Guid EntryId, Guid AssessmentId)> PendingAssessment(Harness f, CreateExternalImpactAssessmentInput impact)
    {
        var entry = await f.Service.CreateAsync(Guideline(), Corr, CancellationToken.None);
        var assessment = await f.Service.CreateImpactAssessmentAsync(entry.Data!.Id, impact, Corr, CancellationToken.None);
        return (entry.Data.Id, assessment.Data!.Id);
    }

    private static ExternalDocumentFieldsInput Guideline() => new(
        ExternalDocumentTitle: "EU GMP Annex 1 — Manufacture of Sterile Medicinal Products",
        ExternalAuthorityName: "European Commission",
        SourceReference: "EudraLex Volume 4, Annex 1",
        ExternalDocumentCode: "EU-GMP-ANNEX-1",
        ExternalDocumentType: "Guideline",
        Jurisdiction: "EU",
        CountryCode: null,
        RegionCode: "EU",
        SourceUrl: "https://health.ec.europa.eu/eudralex-volume-4",
        SourceVersion: "Rev 2",
        SourceEffectiveDate: new DateTimeOffset(2022, 8, 25, 0, 0, 0, TimeSpan.Zero),
        SourcePublishedDate: new DateTimeOffset(2022, 8, 22, 0, 0, 0, TimeSpan.Zero),
        SourceSupersededDate: null,
        SourceStatus: "CurrentEffective",
        MonitoringOwnerUserId: Owner,
        MonitoringOwnerRole: "Group Quality Director",
        MonitoringFunction: "Quality Assurance",
        MonitoringFrequency: "Quarterly");

    private static RecordMonitoringCheckInput Check() => new(
        MonitoringSource: "EudraLex portal",
        EvidenceReference: "MON-2026-07-001",
        ChangeDetected: false,
        ChangeSummary: null,
        SourceVersionObserved: "Rev 2",
        SourceEffectiveDateObserved: null,
        CheckDate: null);

    private static CreateExternalImpactAssessmentInput Impact() => new(
        TriggerType: "VersionChange",
        HasGmpImpact: false, HasGdpImpact: false, HasPvImpact: false, HasRaImpact: false,
        HasBatchReleaseImpact: false, HasTrainingImpact: false, HasDocumentImpact: false,
        ImpactSummary: "Assess Annex 1 revision", TriggerDate: null);

    private static CompleteExternalImpactAssessmentInput Complete() => new(
        AssessmentEvidenceReference: "IA-2026-014",
        RecommendedAction: "NoAction",
        ImpactSummary: "No change required",
        ActionOwnerUserId: Owner,
        ActionOwnerRole: "Group Quality Director",
        ActionDueDate: null,
        ActionReference: null);

    private static DocumentMasterRegisterEntry SeedInternalEntry(Harness f, Guid? tenantId = null)
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
            IsExternalDocument = false,
            RegisterStatus = DocumentRegisterStatus.Active,
            LifecycleStatus = ControlledDocumentLifecycleStatus.Effective,
            PermanentUid = "UID-0000001",
            DocumentCode = "GMG-QMS-SOP-0001"
        };
        f.InternalRegister.Items.Add(e);
        return e;
    }

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var externalDocuments = new FakeExternalDocumentRepo(tenant);
        var checks = new FakeCheckRepo(tenant);
        var assessments = new FakeAssessmentRepo(tenant);
        var links = new FakeLinkRepo(tenant);
        var internalRegister = new FakeInternalRegisterRepo(tenant);
        var service = new ExternalDocumentRegisterService(
            externalDocuments, checks, assessments, links, internalRegister, tenant, new FakeUser());
        return new Harness(service, externalDocuments, checks, assessments, links, internalRegister);
    }

    private sealed record Harness(
        ExternalDocumentRegisterService Service,
        FakeExternalDocumentRepo ExternalDocuments,
        FakeCheckRepo Checks,
        FakeAssessmentRepo Assessments,
        FakeLinkRepo Links,
        FakeInternalRegisterRepo InternalRegister);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444414");
        public string? Email => "fu14@example.test";
        public string? DisplayName => "FU14 Tester";
        public string ActorName => "fu14@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeExternalDocumentRepo(ITenantContext tenant) : IExternalDocumentRegisterRepository
    {
        public List<ExternalDocumentRegisterEntry> Items { get; } = [];
        private IEnumerable<ExternalDocumentRegisterEntry> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<ExternalDocumentRegisterEntry> CreateAsync(ExternalDocumentRegisterEntry e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<ExternalDocumentRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<ExternalDocumentRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(ExternalDocumentRegisterEntry e, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == e.Id);
            if (i >= 0) Items[i] = e;
            return Task.FromResult(i >= 0);
        }

        public Task<IReadOnlyList<ExternalDocumentRegisterEntry>> ListAsync(ExternalDocumentListFilter filter, CancellationToken ct = default)
        {
            var q = Scoped;
            if (filter.ExternalDocumentStatus is { } s) q = q.Where(x => x.ExternalDocumentStatus == s);
            if (filter.SourceStatus is { } ss) q = q.Where(x => x.SourceStatus == ss);
            if (filter.ExternalDocumentType is { } t) q = q.Where(x => x.ExternalDocumentType == t);
            if (filter.ImpactAssessmentStatus is { } ia) q = q.Where(x => x.ImpactAssessmentStatus == ia);
            if (filter.MonitoringOwnerUserId is { } o) q = q.Where(x => x.MonitoringOwnerUserId == o);
            return Task.FromResult<IReadOnlyList<ExternalDocumentRegisterEntry>>(q.ToList());
        }
    }

    private sealed class FakeCheckRepo(ITenantContext tenant) : IExternalDocumentMonitoringCheckRepository
    {
        public List<ExternalDocumentMonitoringCheck> Items { get; } = [];
        private IEnumerable<ExternalDocumentMonitoringCheck> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<ExternalDocumentMonitoringCheck> CreateAsync(ExternalDocumentMonitoringCheck c, CancellationToken ct = default) { Items.Add(c); return Task.FromResult(c); }
        public Task<IReadOnlyList<ExternalDocumentMonitoringCheck>> GetByExternalDocumentAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentMonitoringCheck>>(Scoped.Where(x => x.ExternalDocumentRegisterEntryId == id).ToList());
    }

    private sealed class FakeAssessmentRepo(ITenantContext tenant) : IExternalDocumentImpactAssessmentRepository
    {
        public List<ExternalDocumentImpactAssessment> Items { get; } = [];
        private IEnumerable<ExternalDocumentImpactAssessment> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<ExternalDocumentImpactAssessment> CreateAsync(ExternalDocumentImpactAssessment a, CancellationToken ct = default) { Items.Add(a); return Task.FromResult(a); }
        public Task<ExternalDocumentImpactAssessment?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<ExternalDocumentImpactAssessment>> GetByExternalDocumentAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentImpactAssessment>>(Scoped.Where(x => x.ExternalDocumentRegisterEntryId == id).ToList());
        public Task<IReadOnlyList<ExternalDocumentImpactAssessment>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentImpactAssessment>>(Scoped.ToList());
        public Task<bool> UpdateAsync(ExternalDocumentImpactAssessment a, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == a.Id);
            if (i >= 0) Items[i] = a;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeLinkRepo(ITenantContext tenant) : IExternalDocumentInternalLinkRepository
    {
        public List<ExternalDocumentInternalLink> Items { get; } = [];
        private IEnumerable<ExternalDocumentInternalLink> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<ExternalDocumentInternalLink> CreateAsync(ExternalDocumentInternalLink l, CancellationToken ct = default) { Items.Add(l); return Task.FromResult(l); }
        public Task<IReadOnlyList<ExternalDocumentInternalLink>> GetByExternalDocumentAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentInternalLink>>(Scoped.Where(x => x.ExternalDocumentRegisterEntryId == id).ToList());
        public Task<IReadOnlyList<ExternalDocumentInternalLink>> GetByInternalRegisterEntryAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentInternalLink>>(Scoped.Where(x => x.InternalRegisterEntryId == id).ToList());
        public Task<bool> UpdateAsync(ExternalDocumentInternalLink l, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == l.Id);
            if (i >= 0) Items[i] = l;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeInternalRegisterRepo(ITenantContext tenant) : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];
        private IEnumerable<DocumentMasterRegisterEntry> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string uid, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.PermanentUid == uid));
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.DocumentCode == code));
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.ControlledDocumentId == id));
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry e, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == e.Id);
            if (i >= 0) Items[i] = e;
            return Task.FromResult(i >= 0);
        }
    }
}
