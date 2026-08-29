using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementQualityEvent;
using Diten.Platform.Application.Features.DocumentManagementQualityEvent.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU22 — document-control quality event / deviation / CAPA bridge tests (GMG-QMS-SOP-0001). Tenant-aware
/// in-memory fakes exercise the trigger mapping, the closure gates, the CAPA state machine and bridge idempotency.
///
/// The gate assertions matter most: a critical deviation must never close on an unexamined basis, and an
/// ineffective CAPA must never be closed as though it worked.
/// </summary>
public sealed class DocumentQualityEventCAPATests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid Owner = Guid.Parse("b0000000-0000-0000-0000-000000000022");
    private static readonly Guid RegisterEntryId = Guid.Parse("50000000-0000-0000-0000-000000000022");
    private const string Corr = "fu22-corr-1";

    // ── quality event ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_quality_event()
    {
        var f = Fixture();

        var r = await f.Events.CreateAsync(ManualEvent(), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal("Draft", r.Data!.EventStatus);
        Assert.StartsWith("QE-", r.Data.QualityEventNumber);
    }

    [Fact]
    public async Task Create_quality_event_requires_title_and_description()
    {
        var f = Fixture();

        var noTitle = await f.Events.CreateAsync(ManualEvent() with { EventTitle = " " }, Corr, CancellationToken.None);
        var noDescription = await f.Events.CreateAsync(ManualEvent() with { EventDescription = "" }, Corr, CancellationToken.None);

        Assert.Equal(QualityEventReasonCodes.TitleRequired, noTitle.ReasonCode);
        Assert.Equal(QualityEventReasonCodes.DescriptionRequired, noDescription.ReasonCode);
        Assert.Empty(f.EventRepo.Items);
    }

    [Fact]
    public async Task Non_manual_quality_event_requires_detection_evidence()
    {
        var f = Fixture();

        var r = await f.Events.CreateAsync(ManualEvent() with
        {
            SourceType = nameof(QualityEventSourceType.ObsoleteCopyFinding),
            SourceId = Guid.NewGuid(),
            DetectionEvidenceReference = null
        }, Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(QualityEventReasonCodes.DetectionEvidenceRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Critical_quality_event_requires_deviation_unless_justified()
    {
        var f = Fixture();
        var critical = ManualEvent() with
        {
            EventSeverity = nameof(QualityEventSeverity.Critical),
            RequiresDeviation = false
        };

        var blocked = await f.Events.CreateAsync(critical, Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.CriticalRequiresDeviation, blocked.ReasonCode);

        // A documented waiver is accepted — the decision is recorded, not hidden.
        var waived = await f.Events.CreateAsync(critical with
        {
            DeviationWaiverJustification = "Assessed by GQD as a documentation-only issue with no product impact."
        }, Corr, CancellationToken.None);
        Assert.True(waived.IsSuccessful);
        Assert.NotNull(waived.Data!.DeviationWaiverJustification);

        // Requiring a deviation is of course also accepted.
        var withDeviation = await f.Events.CreateAsync(critical with { RequiresDeviation = true }, Corr, CancellationToken.None);
        Assert.True(withDeviation.IsSuccessful);
    }

    [Fact]
    public async Task Close_quality_event_requires_closure_evidence()
    {
        var f = Fixture();
        var id = await OpenEventAsync(f);

        var r = await f.Events.CloseAsync(id, new CloseQualityEventInput("  ", null), Corr, CancellationToken.None);

        Assert.Equal(QualityEventReasonCodes.ClosureEvidenceRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Close_quality_event_blocks_when_the_required_deviation_is_not_closed()
    {
        var f = Fixture();
        var eventId = await OpenEventAsync(f, requiresDeviation: true);

        // No deviation raised at all.
        var noDeviation = await f.Events.CloseAsync(eventId, Close(), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.DeviationNotClosed, noDeviation.ReasonCode);

        // Deviation raised but still open.
        var deviation = await f.Deviations.CreateAsync(Deviation(eventId), Corr, CancellationToken.None);
        var stillOpen = await f.Events.CloseAsync(eventId, Close(), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.DeviationNotClosed, stillOpen.ReasonCode);

        // Closed deviation unblocks the event.
        await f.Deviations.CloseAsync(deviation.Data!.Id, new CloseDeviationInput("DEV-CLOSE-1", null), Corr, CancellationToken.None);
        var ok = await f.Events.CloseAsync(eventId, Close(), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Equal("Closed", ok.Data!.EventStatus);
    }

    [Fact]
    public async Task Close_quality_event_blocks_when_required_CAPA_is_outstanding()
    {
        var f = Fixture();
        var eventId = await OpenEventAsync(f, requiresCapa: true);

        var noCapa = await f.Events.CloseAsync(eventId, Close(), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.CapaNotSettled, noCapa.ReasonCode);

        var capa = await f.Capa.CreateAsync(Capa(qualityEventId: eventId), Corr, CancellationToken.None);
        var stillOpen = await f.Events.CloseAsync(eventId, Close(), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.CapaNotSettled, stillOpen.ReasonCode);

        await f.Capa.CompleteAsync(capa.Data!.Id, new CompleteCapaActionInput("DONE-1", null), Corr, CancellationToken.None);
        await f.Capa.CloseAsync(capa.Data.Id, new CloseCapaActionInput(null), Corr, CancellationToken.None);

        var ok = await f.Events.CloseAsync(eventId, Close(), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
    }

    [Fact]
    public async Task Cancel_quality_event_requires_a_reason()
    {
        var f = Fixture();
        var id = await OpenEventAsync(f);

        var noReason = await f.Events.CancelAsync(id, new CancelQualityEventInput(" "), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.ReasonRequired, noReason.ReasonCode);

        var ok = await f.Events.CancelAsync(id, new CancelQualityEventInput("Duplicate of QE-123"), Corr, CancellationToken.None);
        Assert.Equal("Cancelled", ok.Data!.EventStatus);
    }

    // ── deviation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_deviation_requires_a_quality_event()
    {
        var f = Fixture();

        var r = await f.Deviations.CreateAsync(Deviation(Guid.NewGuid()), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(QualityEventReasonCodes.DeviationRequiresQualityEvent, r.ReasonCode);
        Assert.Empty(f.DeviationRepo.Items);
    }

    [Fact]
    public async Task Creating_a_deviation_advances_the_quality_event()
    {
        var f = Fixture();
        var eventId = await OpenEventAsync(f, requiresDeviation: true);

        var r = await f.Deviations.CreateAsync(Deviation(eventId), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        var qualityEvent = f.EventRepo.Items.Single();
        Assert.Equal(r.Data!.Id, qualityEvent.DeviationId);
        Assert.Equal(QualityEventStatus.DeviationOpened, qualityEvent.EventStatus);
    }

    [Fact]
    public async Task Critical_deviation_close_requires_root_cause_and_impact_assessment()
    {
        var f = Fixture();
        var eventId = await OpenEventAsync(f, requiresDeviation: true);
        var created = await f.Deviations.CreateAsync(
            Deviation(eventId) with { DeviationSeverity = nameof(QualityDeviationSeverity.Critical) }, Corr, CancellationToken.None);
        var id = created.Data!.Id;

        var noRootCause = await f.Deviations.CloseAsync(id, new CloseDeviationInput("CLOSE-1", null), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.RootCauseRequired, noRootCause.ReasonCode);

        // Root cause recorded, but impact still NotAssessed — "we did not look" is not a closure basis.
        await f.Deviations.RecordInvestigationAsync(id, new RecordDeviationInvestigationInput(
            "Operator used a superseded printout.", nameof(DeviationRootCauseCategory.HumanError),
            null, null, "INV-1"), Corr, CancellationToken.None);

        var noImpact = await f.Deviations.CloseAsync(id, new CloseDeviationInput("CLOSE-1", null), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.ImpactAssessmentRequired, noImpact.ReasonCode);

        await f.Deviations.RecordInvestigationAsync(id, new RecordDeviationInvestigationInput(
            null, null, "No batch affected.", nameof(DeviationImpactAssessment.NoImpact), null), Corr, CancellationToken.None);

        var ok = await f.Deviations.CloseAsync(id, new CloseDeviationInput("CLOSE-1", null), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Equal("Closed", ok.Data!.DeviationStatus);
    }

    [Fact]
    public async Task Minor_deviation_can_close_without_a_root_cause()
    {
        var f = Fixture();
        var eventId = await OpenEventAsync(f, requiresDeviation: true);
        var created = await f.Deviations.CreateAsync(Deviation(eventId), Corr, CancellationToken.None);

        var r = await f.Deviations.CloseAsync(created.Data!.Id, new CloseDeviationInput("CLOSE-1", null), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
    }

    [Fact]
    public async Task Deviation_requiring_CAPA_cannot_close_without_a_settled_CAPA()
    {
        var f = Fixture();
        var eventId = await OpenEventAsync(f, requiresDeviation: true);
        var created = await f.Deviations.CreateAsync(
            Deviation(eventId) with { RequiresCAPA = true }, Corr, CancellationToken.None);
        var id = created.Data!.Id;

        var noCapa = await f.Deviations.CloseAsync(id, new CloseDeviationInput("CLOSE-1", null), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.DeviationRequiresCapa, noCapa.ReasonCode);

        // A documented closure exception is the auditable alternative.
        var withException = await f.Deviations.CloseAsync(id,
            new CloseDeviationInput("CLOSE-1", "CAPA deferred to the annual system review, approved by GQD."),
            Corr, CancellationToken.None);
        Assert.True(withException.IsSuccessful);
        Assert.NotNull(withException.Data!.ClosureExceptionJustification);
    }

    [Fact]
    public async Task Deviation_close_requires_closure_evidence_and_cancel_requires_reason()
    {
        var f = Fixture();
        var eventId = await OpenEventAsync(f, requiresDeviation: true);
        var created = await f.Deviations.CreateAsync(Deviation(eventId), Corr, CancellationToken.None);

        var noEvidence = await f.Deviations.CloseAsync(created.Data!.Id, new CloseDeviationInput("", null), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.ClosureEvidenceRequired, noEvidence.ReasonCode);

        var noReason = await f.Deviations.CancelAsync(created.Data.Id, new CancelDeviationInput(" "), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.ReasonRequired, noReason.ReasonCode);
    }

    // ── CAPA ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_CAPA_requires_a_parent_owner_and_due_date()
    {
        var f = Fixture();
        var eventId = await OpenEventAsync(f);

        var noParent = await f.Capa.CreateAsync(Capa() with { QualityEventId = null, DeviationId = null }, Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.CapaRequiresParent, noParent.ReasonCode);

        var noOwner = await f.Capa.CreateAsync(
            Capa(qualityEventId: eventId) with { ActionOwnerUserId = null, ActionOwnerRole = null }, Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.CapaOwnerRequired, noOwner.ReasonCode);

        var noDueDate = await f.Capa.CreateAsync(
            Capa(qualityEventId: eventId) with { DueDate = null }, Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.CapaDueDateRequired, noDueDate.ReasonCode);

        var ok = await f.Capa.CreateAsync(Capa(qualityEventId: eventId), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.StartsWith("CAPA-", ok.Data!.CAPANumber);
    }

    [Fact]
    public async Task An_immediate_correction_needs_no_due_date()
    {
        var f = Fixture();
        var eventId = await OpenEventAsync(f);

        var r = await f.Capa.CreateAsync(Capa(qualityEventId: eventId) with
        {
            ActionType = nameof(CapaActionType.Correction), DueDate = null
        }, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
    }

    [Fact]
    public async Task Complete_CAPA_requires_completion_evidence()
    {
        var f = Fixture();
        var id = await CapaActionAsync(f);

        var r = await f.Capa.CompleteAsync(id, new CompleteCapaActionInput("  ", null), Corr, CancellationToken.None);

        Assert.Equal(QualityEventReasonCodes.CapaCompletionEvidenceRequired, r.ReasonCode);
    }

    [Fact]
    public async Task CAPA_state_transitions_follow_the_state_machine()
    {
        var f = Fixture();
        var id = await CapaActionAsync(f);

        var started = await f.Capa.StartAsync(id, Corr, CancellationToken.None);
        Assert.Equal("InProgress", started.Data!.ActionStatus);

        var completed = await f.Capa.CompleteAsync(id, new CompleteCapaActionInput("DONE-1", null), Corr, CancellationToken.None);
        Assert.Equal("Completed", completed.Data!.ActionStatus);

        var closed = await f.Capa.CloseAsync(id, new CloseCapaActionInput(null), Corr, CancellationToken.None);
        Assert.Equal("Closed", closed.Data!.ActionStatus);

        // A settled action cannot be restarted.
        var restart = await f.Capa.StartAsync(id, Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.CapaInvalidState, restart.ReasonCode);
    }

    [Fact]
    public async Task CAPA_requiring_effectiveness_cannot_close_until_the_verdict_is_recorded()
    {
        var f = Fixture();
        var id = await CapaActionAsync(f, effectivenessRequired: true);

        var completed = await f.Capa.CompleteAsync(id, new CompleteCapaActionInput("DONE-1", null), Corr, CancellationToken.None);
        Assert.Equal("EffectivenessPending", completed.Data!.ActionStatus);

        var blocked = await f.Capa.CloseAsync(id, new CloseCapaActionInput(null), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.CapaEffectivenessPending, blocked.ReasonCode);

        var effective = await f.Capa.RecordEffectivenessAsync(id,
            new RecordCapaEffectivenessInput(nameof(CapaEffectivenessResult.Effective), "EFF-1", "Verified over two cycles."),
            Corr, CancellationToken.None);
        Assert.Equal("Effective", effective.Data!.ActionStatus);

        var ok = await f.Capa.CloseAsync(id, new CloseCapaActionInput(null), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
    }

    [Fact]
    public async Task Record_effectiveness_requires_evidence_and_a_valid_verdict()
    {
        var f = Fixture();
        var id = await CapaActionAsync(f, effectivenessRequired: true);
        await f.Capa.CompleteAsync(id, new CompleteCapaActionInput("DONE-1", null), Corr, CancellationToken.None);

        var noEvidence = await f.Capa.RecordEffectivenessAsync(id,
            new RecordCapaEffectivenessInput(nameof(CapaEffectivenessResult.Effective), " ", null), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.CapaEffectivenessEvidenceRequired, noEvidence.ReasonCode);

        // "Pending" is not a verdict.
        var notAVerdict = await f.Capa.RecordEffectivenessAsync(id,
            new RecordCapaEffectivenessInput(nameof(CapaEffectivenessResult.Pending), "EFF-1", null), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.ValidationFailed, notAVerdict.ReasonCode);
    }

    /// <summary>
    /// PRODUCT DECISION: an ineffective action can never be closed as effective. Closing it requires a documented
    /// exception, and its deviation is pushed back to CAPARequired so the failure forces new action.
    /// </summary>
    [Fact]
    public async Task Ineffective_CAPA_blocks_close_and_reopens_the_deviation()
    {
        var f = Fixture();
        var eventId = await OpenEventAsync(f, requiresDeviation: true);
        var deviation = await f.Deviations.CreateAsync(
            Deviation(eventId) with { RequiresCAPA = true }, Corr, CancellationToken.None);
        var capa = await f.Capa.CreateAsync(
            Capa(deviationId: deviation.Data!.Id) with { EffectivenessCheckRequired = true }, Corr, CancellationToken.None);
        var id = capa.Data!.Id;

        await f.Capa.CompleteAsync(id, new CompleteCapaActionInput("DONE-1", null), Corr, CancellationToken.None);
        var ineffective = await f.Capa.RecordEffectivenessAsync(id,
            new RecordCapaEffectivenessInput(nameof(CapaEffectivenessResult.Ineffective), "EFF-1", "Recurrence observed."),
            Corr, CancellationToken.None);

        Assert.Equal("Ineffective", ineffective.Data!.ActionStatus);
        // The deviation is pushed back so the failure is not absorbed.
        Assert.Equal(QualityDeviationStatus.CAPARequired, f.DeviationRepo.Items.Single().DeviationStatus);

        var blocked = await f.Capa.CloseAsync(id, new CloseCapaActionInput(null), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.CapaIneffectiveRequiresException, blocked.ReasonCode);

        var withException = await f.Capa.CloseAsync(id,
            new CloseCapaActionInput("Superseded by CAPA-002 which addresses the systemic cause."), Corr, CancellationToken.None);
        Assert.True(withException.IsSuccessful);
        Assert.Equal("Closed", withException.Data!.ActionStatus);
    }

    [Fact]
    public async Task An_incomplete_CAPA_requires_an_exception_to_close()
    {
        var f = Fixture();
        var id = await CapaActionAsync(f);

        var blocked = await f.Capa.CloseAsync(id, new CloseCapaActionInput(null), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.CapaInvalidState, blocked.ReasonCode);

        var ok = await f.Capa.CloseAsync(id, new CloseCapaActionInput("No longer applicable after process change."), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
    }

    [Fact]
    public async Task Cancel_CAPA_requires_a_reason()
    {
        var f = Fixture();
        var id = await CapaActionAsync(f);

        var noReason = await f.Capa.CancelAsync(id, new CancelCapaActionInput(""), Corr, CancellationToken.None);
        Assert.Equal(QualityEventReasonCodes.ReasonRequired, noReason.ReasonCode);

        var ok = await f.Capa.CancelAsync(id, new CancelCapaActionInput("Duplicate action"), Corr, CancellationToken.None);
        Assert.Equal("Cancelled", ok.Data!.ActionStatus);
    }

    // ── trigger mapping ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(ObsoleteCopyFindingType.SuspendedDocumentInUse, "Critical", true, true)]
    [InlineData(ObsoleteCopyFindingType.RetiredCopyAvailable, "Critical", true, false)]
    [InlineData(ObsoleteCopyFindingType.SupersededCopyAtPointOfUse, "Major", true, false)]
    [InlineData(ObsoleteCopyFindingType.UncontrolledCopyDetected, "Critical", true, true)]
    [InlineData(ObsoleteCopyFindingType.MissingCopyDuringReconciliation, "Major", true, false)]
    public void Obsolete_copy_finding_mapping(
        ObsoleteCopyFindingType findingType, string expectedSeverity, bool expectDeviation, bool expectCapa)
    {
        var mapping = DocumentQualityEventTriggerMapper.FromObsoleteCopyFinding(findingType);

        Assert.Equal(expectedSeverity, mapping.EventSeverity);
        Assert.Equal(expectDeviation, mapping.RequiresDeviation);
        Assert.Equal(expectCapa, mapping.RequiresCAPA);
    }

    [Theory]
    [InlineData(GDocPCorrectionType.Reconstruction, false, "Critical", true, true)]
    [InlineData(GDocPCorrectionType.DataIntegrityCorrection, false, "Critical", true, true)]
    [InlineData(GDocPCorrectionType.EvidenceReferenceCorrection, false, "Major", true, false)]
    [InlineData(GDocPCorrectionType.StatusCorrection, false, "Major", true, false)]
    // Backdating is judged on the ACT, not the declared type.
    [InlineData(GDocPCorrectionType.DateCorrection, true, "Critical", true, false)]
    [InlineData(GDocPCorrectionType.TypographicalCorrection, false, "Minor", false, false)]
    public void GDocP_correction_mapping(
        GDocPCorrectionType correctionType, bool isBackdating, string expectedSeverity, bool expectDeviation, bool expectCapa)
    {
        var mapping = DocumentQualityEventTriggerMapper.FromGDocPCorrection(correctionType, isBackdating);

        Assert.Equal(expectedSeverity, mapping.EventSeverity);
        Assert.Equal(expectDeviation, mapping.RequiresDeviation);
        Assert.Equal(expectCapa, mapping.RequiresCAPA);
    }

    [Fact]
    public void Severity_override_may_raise_but_never_lower()
    {
        var major = DocumentQualityEventTriggerMapper.FromObsoleteCopyFinding(ObsoleteCopyFindingType.SupersededCopyAtPointOfUse);

        var raised = DocumentQualityEventTriggerMapper.WithSeverityOverride(major, QualityEventSeverity.Critical);
        Assert.Equal("Critical", raised.EventSeverity);
        Assert.True(raised.RequiresDeviation);

        var lowered = DocumentQualityEventTriggerMapper.WithSeverityOverride(major, QualityEventSeverity.Minor);
        Assert.Equal("Major", lowered.EventSeverity);
        Assert.Contains("may only raise severity", lowered.MappingRationale);
    }

    // ── bridges ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bridge_from_GDocP_reconstruction_creates_a_critical_event_requiring_deviation_and_CAPA()
    {
        var f = Fixture();
        var correction = SeedCorrection(f, GDocPCorrectionType.Reconstruction);

        var r = await f.Bridge.FromGDocPCorrectionAsync(correction.Id, null, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Critical", r.Data!.EventSeverity);
        Assert.Equal("DataIntegrityConcern", r.Data.EventType);
        Assert.True(r.Data.RequiresDeviation);
        Assert.True(r.Data.RequiresCAPA);
        Assert.Equal("GDocPCorrection", r.Data.SourceType);
        // The existing free-text reference is snapshotted onto the link, never removed from the source.
        var link = Assert.Single(f.LinkRepo.Items);
        Assert.Equal("DEV-EXISTING-1", link.SourceReferenceSnapshot);
        Assert.Equal("DEV-EXISTING-1", f.CorrectionRepo.Items.Single().DeviationReference);
    }

    [Fact]
    public async Task Bridge_from_GDocP_backdating_creates_a_critical_deviation_requirement()
    {
        var f = Fixture();
        var correction = SeedCorrection(f, GDocPCorrectionType.DateCorrection, isBackdating: true);

        var r = await f.Bridge.FromGDocPCorrectionAsync(correction.Id, null, Corr, CancellationToken.None);

        Assert.Equal("Critical", r.Data!.EventSeverity);
        Assert.True(r.Data.RequiresDeviation);
    }

    [Fact]
    public async Task Bridge_from_uncontrolled_copy_creates_a_critical_event_requiring_CAPA()
    {
        var f = Fixture();
        var finding = SeedFinding(f, ObsoleteCopyFindingType.UncontrolledCopyDetected);

        var r = await f.Bridge.FromObsoleteCopyFindingAsync(finding.Id, null, Corr, CancellationToken.None);

        Assert.Equal("Critical", r.Data!.EventSeverity);
        Assert.Equal("UncontrolledCopyDetected", r.Data.EventType);
        Assert.True(r.Data.RequiresCAPA);
        Assert.True(r.Data.ImmediateContainmentRequired);
    }

    [Fact]
    public async Task Bridge_from_superseded_copy_creates_a_major_deviation_requirement()
    {
        var f = Fixture();
        var finding = SeedFinding(f, ObsoleteCopyFindingType.SupersededCopyAtPointOfUse);

        var r = await f.Bridge.FromObsoleteCopyFindingAsync(finding.Id, null, Corr, CancellationToken.None);

        Assert.Equal("Major", r.Data!.EventSeverity);
        Assert.True(r.Data.RequiresDeviation);
        Assert.False(r.Data.RequiresCAPA);
        Assert.Equal(RegisterEntryId, r.Data.RegisterEntryId);
    }

    [Fact]
    public async Task Bridge_from_overdue_temporary_issue_creates_a_major_deviation_requirement()
    {
        var f = Fixture();
        var issue = SeedTemporaryIssue(f, TemporaryIssueStatus.Overdue);

        var r = await f.Bridge.FromTemporaryIssueAsync(issue.Id, null, Corr, CancellationToken.None);

        Assert.Equal("Major", r.Data!.EventSeverity);
        Assert.Equal("MissingReconciliation", r.Data.EventType);
        Assert.True(r.Data.RequiresDeviation);
    }

    [Fact]
    public async Task Bridge_from_external_impact_quality_event_review_creates_an_event()
    {
        var f = Fixture();
        var assessment = SeedExternalImpact(f, ExternalImpactRecommendedAction.QualityEventReview);

        var r = await f.Bridge.FromExternalImpactAssessmentAsync(assessment.Id, null, Corr, CancellationToken.None);

        Assert.Equal("ExternalRegulatoryImpact", r.Data!.EventType);
        Assert.True(r.Data.RequiresDeviation);
        Assert.Equal(assessment.ExternalDocumentRegisterEntryId, r.Data.ExternalDocumentId);
    }

    [Fact]
    public async Task Bridge_is_idempotent_for_the_same_source()
    {
        var f = Fixture();
        var finding = SeedFinding(f, ObsoleteCopyFindingType.SupersededCopyAtPointOfUse);

        var first = await f.Bridge.FromObsoleteCopyFindingAsync(finding.Id, null, Corr, CancellationToken.None);
        var second = await f.Bridge.FromObsoleteCopyFindingAsync(finding.Id, null, Corr, CancellationToken.None);

        Assert.Equal(first.Data!.Id, second.Data!.Id);
        Assert.Single(f.EventRepo.Items);
        Assert.Single(f.LinkRepo.Items);
    }

    [Fact]
    public async Task A_closed_event_allows_the_same_source_to_raise_a_new_one()
    {
        var f = Fixture();
        var finding = SeedFinding(f, ObsoleteCopyFindingType.SupersededCopyAtPointOfUse);
        var first = await f.Bridge.FromObsoleteCopyFindingAsync(finding.Id, null, Corr, CancellationToken.None);

        // Settle the first event's deviation requirement, then close it.
        var deviation = await f.Deviations.CreateAsync(Deviation(first.Data!.Id), Corr, CancellationToken.None);
        await f.Deviations.CloseAsync(deviation.Data!.Id, new CloseDeviationInput("DEV-CLOSE-1", null), Corr, CancellationToken.None);
        await f.Events.CloseAsync(first.Data.Id, Close(), Corr, CancellationToken.None);

        var second = await f.Bridge.FromObsoleteCopyFindingAsync(finding.Id, null, Corr, CancellationToken.None);

        Assert.NotEqual(first.Data.Id, second.Data!.Id);
        Assert.Equal(2, f.EventRepo.Items.Count);
    }

    [Fact]
    public async Task Bridge_from_an_unknown_source_is_refused()
    {
        var f = Fixture();

        var r = await f.Bridge.FromObsoleteCopyFindingAsync(Guid.NewGuid(), null, Corr, CancellationToken.None);

        Assert.Equal(404, r.StatusCode);
        Assert.Equal(QualityEventReasonCodes.SourceNotFound, r.ReasonCode);
        Assert.Empty(f.EventRepo.Items);
    }

    [Fact]
    public async Task Generic_bridge_requires_detection_evidence()
    {
        var f = Fixture();

        var r = await f.Bridge.FromSourceAsync(new BridgeFromSourceInput(
            nameof(QualityEventSourceType.SuspensionCase), Guid.NewGuid(), "Suspended for data integrity", null, null),
            Corr, CancellationToken.None);

        Assert.Equal(QualityEventReasonCodes.DetectionEvidenceRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Generic_bridge_from_suspension_case_creates_a_major_event()
    {
        var f = Fixture();

        var r = await f.Bridge.FromSourceAsync(new BridgeFromSourceInput(
            nameof(QualityEventSourceType.SuspensionCase), Guid.NewGuid(), "Suspended pending investigation",
            null, "SUSP-EVIDENCE-1"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("SuspensionTrigger", r.Data!.EventType);
        Assert.True(r.Data.RequiresDeviation);
    }

    [Fact]
    public async Task Source_link_history_is_preserved_when_the_event_closes()
    {
        var f = Fixture();
        var finding = SeedFinding(f, ObsoleteCopyFindingType.SupersededCopyAtPointOfUse);
        var created = await f.Bridge.FromObsoleteCopyFindingAsync(finding.Id, null, Corr, CancellationToken.None);
        var deviation = await f.Deviations.CreateAsync(Deviation(created.Data!.Id), Corr, CancellationToken.None);
        await f.Deviations.CloseAsync(deviation.Data!.Id, new CloseDeviationInput("DEV-CLOSE-1", null), Corr, CancellationToken.None);

        await f.Events.CloseAsync(created.Data.Id, Close(), Corr, CancellationToken.None);

        var link = Assert.Single(f.LinkRepo.Items);
        Assert.Equal(QualityEventSourceLinkStatus.Closed, link.LinkStatus);
        Assert.False(link.IsDeleted);
    }

    // ── FU15 retention integration ────────────────────────────────────────────

    [Fact]
    public void Retention_subject_types_appended_without_shifting_existing_ordinals()
    {
        Assert.Equal(27, (int)RetentionSubjectType.Other);
        Assert.Equal(36, (int)RetentionSubjectType.GDocPCorrectionReview);
        Assert.Equal(37, (int)RetentionSubjectType.DocumentQualityEvent);
        Assert.Equal(38, (int)RetentionSubjectType.DocumentDeviation);
        Assert.Equal(39, (int)RetentionSubjectType.DocumentCAPAAction);
        Assert.Equal(40, (int)RetentionSubjectType.DocumentQualityEventSourceLink);
    }

    /// <summary>
    /// The FU22 quality deviation vocabulary must stay distinct from MOD-0028-FU09's collection-tree deviation
    /// vocabulary, which lives in the same namespace.
    /// </summary>
    [Fact]
    public void Quality_deviation_enums_do_not_collide_with_the_MOD0028_collection_deviation_enums()
    {
        // MOD-0028-FU09's DeviationSeverity has an Info level; the GxP one deliberately does not.
        Assert.Equal(4, Enum.GetValues<DeviationSeverity>().Length);
        Assert.Contains(DeviationSeverity.Info, Enum.GetValues<DeviationSeverity>());

        Assert.Equal(3, Enum.GetValues<QualityDeviationSeverity>().Length);
        Assert.DoesNotContain("Info", Enum.GetNames<QualityDeviationSeverity>());

        // The two status vocabularies are different shapes entirely.
        Assert.DoesNotContain("UnderInvestigation", Enum.GetNames<DeviationStatus>());
        Assert.Contains("UnderInvestigation", Enum.GetNames<QualityDeviationStatus>());
    }

    // ── isolation / durability ────────────────────────────────────────────────

    [Fact]
    public async Task Cross_tenant_quality_event_is_blocked()
    {
        var f = Fixture();
        var foreign = new DocumentQualityEvent
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, QualityEventNumber = "QE-FOREIGN",
            EventTitle = "Foreign", EventDescription = "Foreign event"
        };
        f.EventRepo.Items.Add(foreign);

        var read = await f.Events.GetAsync(foreign.Id, Corr, CancellationToken.None);
        var close = await f.Events.CloseAsync(foreign.Id, Close(), Corr, CancellationToken.None);

        Assert.Equal(404, read.StatusCode);
        Assert.Equal(404, close.StatusCode);
        Assert.Equal(QualityEventStatus.Draft, f.EventRepo.Items.Single(x => x.Id == foreign.Id).EventStatus);
    }

    [Fact]
    public async Task Cross_tenant_deviation_and_CAPA_are_blocked()
    {
        var f = Fixture();
        var foreignDeviation = new DocumentDeviation
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, DeviationNumber = "DEV-FOREIGN",
            QualityEventId = Guid.NewGuid(), DeviationTitle = "Foreign", DeviationDescription = "Foreign"
        };
        var foreignCapa = new DocumentCAPAAction
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, CAPANumber = "CAPA-FOREIGN",
            ActionTitle = "Foreign", ActionDescription = "Foreign"
        };
        f.DeviationRepo.Items.Add(foreignDeviation);
        f.CapaRepo.Items.Add(foreignCapa);

        Assert.Equal(404, (await f.Deviations.GetAsync(foreignDeviation.Id, Corr, CancellationToken.None)).StatusCode);
        Assert.Equal(404, (await f.Capa.GetAsync(foreignCapa.Id, Corr, CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task A_deviation_cannot_be_raised_against_a_foreign_tenants_quality_event()
    {
        var f = Fixture();
        var foreign = new DocumentQualityEvent
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, QualityEventNumber = "QE-FOREIGN",
            EventTitle = "Foreign", EventDescription = "Foreign event"
        };
        f.EventRepo.Items.Add(foreign);

        var r = await f.Deviations.CreateAsync(Deviation(foreign.Id), Corr, CancellationToken.None);

        Assert.Equal(404, r.StatusCode);
        Assert.Empty(f.DeviationRepo.Items);
    }

    [Fact]
    public async Task A_full_quality_cycle_deletes_nothing()
    {
        var f = Fixture();
        var finding = SeedFinding(f, ObsoleteCopyFindingType.UncontrolledCopyDetected);
        var qualityEvent = await f.Bridge.FromObsoleteCopyFindingAsync(finding.Id, null, Corr, CancellationToken.None);
        var deviation = await f.Deviations.CreateAsync(
            Deviation(qualityEvent.Data!.Id) with { RequiresCAPA = true }, Corr, CancellationToken.None);
        var capa = await f.Capa.CreateAsync(Capa(deviationId: deviation.Data!.Id), Corr, CancellationToken.None);
        await f.Capa.CompleteAsync(capa.Data!.Id, new CompleteCapaActionInput("DONE-1", null), Corr, CancellationToken.None);
        await f.Capa.CloseAsync(capa.Data.Id, new CloseCapaActionInput(null), Corr, CancellationToken.None);
        await f.Deviations.CloseAsync(deviation.Data.Id, new CloseDeviationInput("DEV-CLOSE-1", null), Corr, CancellationToken.None);
        await f.Events.CloseAsync(qualityEvent.Data.Id, Close(), Corr, CancellationToken.None);

        Assert.NotEmpty(f.EventRepo.Items);
        Assert.NotEmpty(f.DeviationRepo.Items);
        Assert.NotEmpty(f.CapaRepo.Items);
        Assert.NotEmpty(f.LinkRepo.Items);
        Assert.DoesNotContain(f.EventRepo.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.DeviationRepo.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.CapaRepo.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.LinkRepo.Items, x => x.IsDeleted);
        // The source finding is untouched by the bridge.
        Assert.Single(f.FindingRepo.Items);
        Assert.False(f.FindingRepo.Items.Single().IsDeleted);
    }

    [Fact]
    public void No_quality_repository_contract_exposes_a_delete_operation()
    {
        var contracts = new[]
        {
            typeof(IDocumentQualityEventRepository), typeof(IDocumentDeviationRepository),
            typeof(IDocumentCAPAActionRepository), typeof(IDocumentQualityEventSourceLinkRepository)
        };

        foreach (var contract in contracts)
        {
            Assert.DoesNotContain(contract.GetMethods(), m =>
                m.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                || m.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase)
                || m.Name.Contains("Purge", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>No FU22 aggregate can carry document content — investigations and evidence are references.</summary>
    [Fact]
    public void No_quality_aggregate_exposes_a_binary_content_property()
    {
        var types = new[]
        {
            typeof(DocumentQualityEvent), typeof(DocumentDeviation), typeof(DocumentCAPAAction),
            typeof(DocumentQualityEventSourceLink)
        };

        foreach (var type in types)
        {
            Assert.DoesNotContain(type.GetProperties(), p =>
                p.PropertyType == typeof(byte[]) || p.PropertyType == typeof(Stream) || p.PropertyType == typeof(Memory<byte>));
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> OpenEventAsync(Harness f, bool requiresDeviation = false, bool requiresCapa = false)
    {
        var created = await f.Events.CreateAsync(
            ManualEvent() with { RequiresDeviation = requiresDeviation, RequiresCAPA = requiresCapa },
            Corr, CancellationToken.None);
        await f.Events.OpenAsync(created.Data!.Id, Corr, CancellationToken.None);
        return created.Data.Id;
    }

    private async Task<Guid> CapaActionAsync(Harness f, bool effectivenessRequired = false)
    {
        var eventId = await OpenEventAsync(f);
        var created = await f.Capa.CreateAsync(
            Capa(qualityEventId: eventId) with { EffectivenessCheckRequired = effectivenessRequired },
            Corr, CancellationToken.None);
        return created.Data!.Id;
    }

    private static CloseQualityEventInput Close() => new("QE-CLOSE-1", "Assessed and closed");

    private static CreateQualityEventInput ManualEvent() => new(
        EventTitle: "Superseded SOP found at the packaging line",
        EventDescription: "A superseded revision of GMG-QMS-SOP-0001 was found in use at the packaging line.",
        EventType: nameof(QualityEventType.ObsoleteCopyUse),
        EventSeverity: nameof(QualityEventSeverity.Major),
        SourceType: nameof(QualityEventSourceType.Manual),
        SourceId: null,
        DetectionEvidenceReference: null,
        RegisterEntryId: RegisterEntryId,
        ControlledDocumentId: null,
        TemplateVariantId: null,
        ExternalDocumentId: null,
        DetectedBy: null,
        ImmediateContainmentRequired: false,
        ImmediateContainmentSummary: null,
        RequiresDeviation: false,
        RequiresCAPA: false,
        DeviationWaiverJustification: null,
        DeviationWaiverEvidenceReference: null,
        ExternalQualitySystemReference: null);

    private static CreateDeviationInput Deviation(Guid qualityEventId) => new(
        QualityEventId: qualityEventId,
        DeviationTitle: "Superseded document in use",
        DeviationDescription: "A superseded revision was available and used at a point of use.",
        DeviationCategory: nameof(QualityDeviationCategory.ControlledCopy),
        DeviationSeverity: nameof(QualityDeviationSeverity.Major),
        OccurredAt: null,
        ReportedBy: null,
        RequiresCAPA: false);

    private static CreateCapaActionInput Capa(Guid? qualityEventId = null, Guid? deviationId = null) => new(
        QualityEventId: qualityEventId,
        DeviationId: deviationId,
        ActionType: nameof(CapaActionType.CorrectiveAction),
        ActionTitle: "Re-train the packaging line on copy control",
        ActionDescription: "Deliver refresher training and re-verify the point-of-use copy register.",
        ActionOwnerUserId: Owner,
        ActionOwnerRole: "QA Documentation",
        DueDate: DateTimeOffset.UtcNow.AddDays(30),
        EffectivenessCheckRequired: false,
        EffectivenessDueDate: null,
        RelatedRegisterEntryIds: null,
        RelatedControlledDocumentIds: null,
        RelatedExternalDocumentIds: null);

    private static DocumentObsoleteCopyFinding SeedFinding(Harness f, ObsoleteCopyFindingType findingType)
    {
        var finding = new DocumentObsoleteCopyFinding
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RegisterEntryId = RegisterEntryId,
            FindingKey = $"FIND-{findingType}",
            FindingType = findingType,
            Severity = ObsoleteCopyFindingSeverity.Major,
            Status = ObsoleteCopyFindingStatus.Open,
            Description = $"{findingType} detected during reconciliation.",
            QualityEventReference = "QE-EXISTING-1"
        };
        f.FindingRepo.Items.Add(finding);
        return finding;
    }

    private static DocumentGDocPCorrectionRecord SeedCorrection(
        Harness f, GDocPCorrectionType correctionType, bool isBackdating = false)
    {
        var correction = new DocumentGDocPCorrectionRecord
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CorrectionNumber = "GDC-20260720-ABCDEF12",
            SubjectId = RegisterEntryId,
            RegisterEntryId = RegisterEntryId,
            FieldPath = "EffectiveDate",
            PreviousValueSnapshot = "2026-07-15T00:00:00Z",
            NewValueSnapshot = "2026-07-01T00:00:00Z",
            CorrectionType = correctionType,
            CorrectionReason = "Corrected per investigation.",
            IsBackdatingCorrection = isBackdating,
            IsHighRiskCorrection = true,
            CorrectionEvidenceReference = "GDC-EV-1",
            DeviationReference = "DEV-EXISTING-1"
        };
        f.CorrectionRepo.Items.Add(correction);
        return correction;
    }

    private static DocumentTemporaryControlledIssue SeedTemporaryIssue(Harness f, TemporaryIssueStatus status)
    {
        var issue = new DocumentTemporaryControlledIssue
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            DowntimeEventId = Guid.NewGuid(),
            RegisterEntryId = RegisterEntryId,
            IssueNumber = "TCI-20260720-ABCDEF12",
            IssueStatus = status,
            IssueReason = "Batch in progress required the effective SOP.",
            ApprovalEvidenceReference = "APPR-1"
        };
        f.IssueRepo.Items.Add(issue);
        return issue;
    }

    private static ExternalDocumentImpactAssessment SeedExternalImpact(Harness f, ExternalImpactRecommendedAction action)
    {
        var assessment = new ExternalDocumentImpactAssessment
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ExternalDocumentRegisterEntryId = Guid.NewGuid(),
            RecommendedAction = action,
            ImpactSummary = "Annex 1 revision affects sterile manufacturing SOPs.",
            AssessmentEvidenceReference = "IA-1",
            DueDate = DateTimeOffset.UtcNow.AddDays(10)
        };
        f.ExternalImpactRepo.Items.Add(assessment);
        return assessment;
    }

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var user = new FakeUser();

        var events = new FakeEventRepo(tenant);
        var deviations = new FakeDeviationRepo(tenant);
        var capa = new FakeCapaRepo(tenant);
        var links = new FakeLinkRepo(tenant);
        var findings = new FakeFindingRepo(tenant);
        var issues = new FakeIssueRepo(tenant);
        var corrections = new FakeCorrectionRepo(tenant);
        var externalImpacts = new FakeExternalImpactRepo(tenant);

        var eventService = new DocumentQualityEventService(events, deviations, capa, links, tenant, user);
        var deviationService = new DocumentDeviationService(deviations, events, capa, eventService, tenant, user);
        var capaService = new DocumentCapaActionService(capa, events, deviations, eventService, deviationService, tenant, user);
        var bridge = new DocumentQualityEventBridgeService(
            eventService, events, links, findings, issues, corrections, externalImpacts, tenant, user);

        return new Harness(eventService, deviationService, capaService, bridge,
            events, deviations, capa, links, findings, issues, corrections, externalImpacts);
    }

    private sealed record Harness(
        DocumentQualityEventService Events,
        DocumentDeviationService Deviations,
        DocumentCapaActionService Capa,
        DocumentQualityEventBridgeService Bridge,
        FakeEventRepo EventRepo,
        FakeDeviationRepo DeviationRepo,
        FakeCapaRepo CapaRepo,
        FakeLinkRepo LinkRepo,
        FakeFindingRepo FindingRepo,
        FakeIssueRepo IssueRepo,
        FakeCorrectionRepo CorrectionRepo,
        FakeExternalImpactRepo ExternalImpactRepo);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444422");
        public string? Email => "fu22@example.test";
        public string? DisplayName => "FU22 Tester";
        public string ActorName => "fu22@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeEventRepo(ITenantContext tenant) : IDocumentQualityEventRepository
    {
        public List<DocumentQualityEvent> Items { get; } = [];
        private IEnumerable<DocumentQualityEvent> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentQualityEvent> CreateAsync(DocumentQualityEvent e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentQualityEvent?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentQualityEvent>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentQualityEvent>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<IReadOnlyList<DocumentQualityEvent>> GetOpenAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentQualityEvent>>(Scoped.Where(x => !x.IsSettled()).ToList());
        public Task<IReadOnlyList<DocumentQualityEvent>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentQualityEvent>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentQualityEvent e, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == e.Id);
            if (i >= 0) Items[i] = e;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeDeviationRepo(ITenantContext tenant) : IDocumentDeviationRepository
    {
        public List<DocumentDeviation> Items { get; } = [];
        private IEnumerable<DocumentDeviation> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentDeviation> CreateAsync(DocumentDeviation d, CancellationToken ct = default) { Items.Add(d); return Task.FromResult(d); }
        public Task<DocumentDeviation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentDeviation>> GetByQualityEventAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentDeviation>>(Scoped.Where(x => x.QualityEventId == id).ToList());
        public Task<IReadOnlyList<DocumentDeviation>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentDeviation>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentDeviation d, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == d.Id);
            if (i >= 0) Items[i] = d;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeCapaRepo(ITenantContext tenant) : IDocumentCAPAActionRepository
    {
        public List<DocumentCAPAAction> Items { get; } = [];
        private IEnumerable<DocumentCAPAAction> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentCAPAAction> CreateAsync(DocumentCAPAAction a, CancellationToken ct = default) { Items.Add(a); return Task.FromResult(a); }
        public Task<DocumentCAPAAction?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentCAPAAction>> GetByQualityEventAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentCAPAAction>>(Scoped.Where(x => x.QualityEventId == id).ToList());
        public Task<IReadOnlyList<DocumentCAPAAction>> GetByDeviationAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentCAPAAction>>(Scoped.Where(x => x.DeviationId == id).ToList());
        public Task<IReadOnlyList<DocumentCAPAAction>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentCAPAAction>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentCAPAAction a, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == a.Id);
            if (i >= 0) Items[i] = a;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeLinkRepo(ITenantContext tenant) : IDocumentQualityEventSourceLinkRepository
    {
        public List<DocumentQualityEventSourceLink> Items { get; } = [];
        private IEnumerable<DocumentQualityEventSourceLink> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentQualityEventSourceLink> CreateAsync(DocumentQualityEventSourceLink l, CancellationToken ct = default) { Items.Add(l); return Task.FromResult(l); }
        public Task<IReadOnlyList<DocumentQualityEventSourceLink>> GetByQualityEventAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentQualityEventSourceLink>>(Scoped.Where(x => x.QualityEventId == id).ToList());
        public Task<IReadOnlyList<DocumentQualityEventSourceLink>> GetBySourceAsync(
            QualityEventSourceType sourceType, Guid sourceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentQualityEventSourceLink>>(
                Scoped.Where(x => x.SourceType == sourceType && x.SourceId == sourceId).ToList());
        public Task<bool> UpdateAsync(DocumentQualityEventSourceLink l, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == l.Id);
            if (i >= 0) Items[i] = l;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeFindingRepo(ITenantContext tenant) : IDocumentObsoleteCopyFindingRepository
    {
        public List<DocumentObsoleteCopyFinding> Items { get; } = [];
        private IEnumerable<DocumentObsoleteCopyFinding> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentObsoleteCopyFinding> CreateAsync(DocumentObsoleteCopyFinding f, CancellationToken ct = default) { Items.Add(f); return Task.FromResult(f); }
        public Task<DocumentObsoleteCopyFinding?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentObsoleteCopyFinding>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentObsoleteCopyFinding>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<bool> UpdateAsync(DocumentObsoleteCopyFinding f, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == f.Id);
            if (i >= 0) Items[i] = f;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeIssueRepo(ITenantContext tenant) : IDocumentTemporaryControlledIssueRepository
    {
        public List<DocumentTemporaryControlledIssue> Items { get; } = [];
        private IEnumerable<DocumentTemporaryControlledIssue> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentTemporaryControlledIssue> CreateAsync(DocumentTemporaryControlledIssue i, CancellationToken ct = default) { Items.Add(i); return Task.FromResult(i); }
        public Task<DocumentTemporaryControlledIssue?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetByDowntimeEventAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTemporaryControlledIssue>>(Scoped.Where(x => x.DowntimeEventId == id).ToList());
        public Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTemporaryControlledIssue>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetOutstandingAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTemporaryControlledIssue>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentTemporaryControlledIssue i, CancellationToken ct = default)
        {
            var idx = Items.FindIndex(x => x.Id == i.Id);
            if (idx >= 0) Items[idx] = i;
            return Task.FromResult(idx >= 0);
        }
    }

    private sealed class FakeCorrectionRepo(ITenantContext tenant) : IDocumentGDocPCorrectionRecordRepository
    {
        public List<DocumentGDocPCorrectionRecord> Items { get; } = [];
        private IEnumerable<DocumentGDocPCorrectionRecord> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentGDocPCorrectionRecord> CreateAsync(DocumentGDocPCorrectionRecord r, CancellationToken ct = default) { Items.Add(r); return Task.FromResult(r); }
        public Task<DocumentGDocPCorrectionRecord?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetBySubjectAsync(GDocPSubjectType t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionRecord>>(Scoped.Where(x => x.SubjectType == t && x.SubjectId == id).ToList());
        public Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetPendingReviewAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionRecord>>([]);
        public Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionRecord>>(Scoped.ToList());
        public Task<bool> UpdateReviewAsync(DocumentGDocPCorrectionRecord r, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeExternalImpactRepo(ITenantContext tenant) : IExternalDocumentImpactAssessmentRepository
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
}
