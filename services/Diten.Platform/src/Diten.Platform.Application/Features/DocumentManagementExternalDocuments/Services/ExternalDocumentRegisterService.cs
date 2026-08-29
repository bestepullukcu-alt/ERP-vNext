using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementExternalDocuments.Services;

/// <summary>
/// MOD-0029-FU14 — External Document Register orchestration (GMG-QMS-SOP-0001 §10). Registers external regulations,
/// guidelines, standards, pharmacopeia and authority communications; records the monitoring evidence trail; raises
/// impact assessments with the 10-working-day regulated deadline; and links external requirements to the internal
/// FU06 register.
///
/// HARD BOUNDARIES enforced here:
/// • An external document is never authored, edited, versioned or made Effective as an internal controlled document.
/// • <c>SourceUrl</c> is a reference only — no crawler, no authority API, no file ingestion, no content bytes.
/// • A completed assessment produces a RECOMMENDATION; it never transitions, suspends or retires an internal
///   document (that stays with the FU08 lifecycle engine and the FU13 suspension engine).
/// • A DraftConsultation source is regulatory intelligence only and can never be promoted to CurrentEffective
///   without a source effective date or decision evidence.
/// • Nothing is hard-deleted: supersession, archival and link closure are status changes.
/// </summary>
public sealed class ExternalDocumentRegisterService
{
    private readonly IExternalDocumentRegisterRepository _externalDocuments;
    private readonly IExternalDocumentMonitoringCheckRepository _checks;
    private readonly IExternalDocumentImpactAssessmentRepository _assessments;
    private readonly IExternalDocumentInternalLinkRepository _links;
    private readonly IDocumentMasterRegisterRepository _internalRegister;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public ExternalDocumentRegisterService(
        IExternalDocumentRegisterRepository externalDocuments,
        IExternalDocumentMonitoringCheckRepository checks,
        IExternalDocumentImpactAssessmentRepository assessments,
        IExternalDocumentInternalLinkRepository links,
        IDocumentMasterRegisterRepository internalRegister,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _externalDocuments = externalDocuments;
        _checks = checks;
        _assessments = assessments;
        _links = links;
        _internalRegister = internalRegister;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    // ── register ──────────────────────────────────────────────────────────────

    public async Task<Response<ExternalDocumentModel>> CreateAsync(ExternalDocumentFieldsInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        if (Validate(input) is { } validationFailure)
        {
            return Fail(validationFailure.Message, 400, validationFailure.ReasonCode, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var frequency = ExternalDocumentWire.ParseFrequency(input.MonitoringFrequency)!.Value;
        var entry = new ExternalDocumentRegisterEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExternalDocumentTitle = input.ExternalDocumentTitle.Trim(),
            ExternalAuthorityName = input.ExternalAuthorityName.Trim(),
            SourceReference = input.SourceReference.Trim(),
            MonitoringFrequency = frequency,
            NextCheckDueDate = ExternalDocumentScheduleCalculator.NextCheckDueDate(frequency, now),
            ExternalDocumentStatus = ExternalDocumentStatus.Active,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        ApplyFields(entry, input);
        ApplyImpactRollup(entry, now);
        await _externalDocuments.CreateAsync(entry, ct);
        return Response<ExternalDocumentModel>.Success(ExternalDocumentWire.ToModel(entry, now), 201, correlationId);
    }

    public async Task<Response<ExternalDocumentModel>> UpdateAsync(Guid id, ExternalDocumentFieldsInput input, string correlationId, CancellationToken ct)
    {
        var (fail, entry) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (entry!.ExternalDocumentStatus == ExternalDocumentStatus.Archived)
        {
            return Fail("An archived external document cannot be edited.", 409, ExternalDocumentReasonCodes.ArchivedNotEditable, correlationId);
        }

        if (Validate(input) is { } validationFailure)
        {
            return Fail(validationFailure.Message, 400, validationFailure.ReasonCode, correlationId);
        }

        var requestedStatus = ExternalDocumentWire.ParseSourceStatus(input.SourceStatus)!.Value;

        // SOP §10.4 — a draft/consultation source cannot silently become an effective requirement.
        if (entry.SourceStatus == ExternalSourceStatus.DraftConsultation
            && requestedStatus == ExternalSourceStatus.CurrentEffective
            && input.SourceEffectiveDate is null
            && string.IsNullOrWhiteSpace(input.PromotionEvidenceReference))
        {
            return Fail(
                "A draft/consultation source requires a source effective date or decision evidence before it can be marked CurrentEffective.",
                409, ExternalDocumentReasonCodes.EffectivePromotionEvidenceRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var previousFrequency = entry.MonitoringFrequency;
        var frequency = ExternalDocumentWire.ParseFrequency(input.MonitoringFrequency)!.Value;

        entry.ExternalDocumentTitle = input.ExternalDocumentTitle.Trim();
        entry.ExternalAuthorityName = input.ExternalAuthorityName.Trim();
        entry.SourceReference = input.SourceReference.Trim();
        entry.MonitoringFrequency = frequency;
        ApplyFields(entry, input);

        // The cadence changed → re-derive the next due date from the last check (or now, if never checked).
        if (previousFrequency != frequency)
        {
            entry.NextCheckDueDate = ExternalDocumentScheduleCalculator.NextCheckDueDate(frequency, entry.LastCheckedAt ?? now);
        }

        ApplyImpactRollup(entry, now);

        // SOP §10.4 — a withdrawn source with live internal links is an action, not a filing update.
        if (entry.SourceStatus == ExternalSourceStatus.Withdrawn && await HasOpenLinksAsync(entry.Id, ct))
        {
            entry.ExternalDocumentStatus = ExternalDocumentStatus.ActionRequired;
            await RaiseImpactAssessmentAsync(entry, ExternalImpactTriggerType.RegulatoryAlert, now, ct);
        }

        Touch(entry, now);
        await _externalDocuments.UpdateAsync(entry, ct);
        return Response<ExternalDocumentModel>.Success(ExternalDocumentWire.ToModel(entry, now), correlationId: correlationId);
    }

    public async Task<Response<ExternalDocumentModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, entry) = await LoadAsync(id, correlationId, ct);
        return fail ?? Response<ExternalDocumentModel>.Success(ExternalDocumentWire.ToModel(entry!, DateTimeOffset.UtcNow), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<ExternalDocumentModel>>> ListAsync(ExternalDocumentListFilter filter, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var now = DateTimeOffset.UtcNow;
        var rows = await _externalDocuments.ListAsync(filter, ct);
        return Response<IReadOnlyList<ExternalDocumentModel>>.Success(
            rows.Select(x => ExternalDocumentWire.ToModel(x, now)).ToList(), correlationId: correlationId);
    }

    public async Task<Response<ExternalDocumentModel>> MarkSupersededAsync(Guid id, MarkExternalDocumentSupersededInput input, string correlationId, CancellationToken ct)
    {
        var (fail, entry) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        var now = DateTimeOffset.UtcNow;
        entry!.SourceStatus = ExternalSourceStatus.Superseded;
        entry.SourceSupersededDate = input.SourceSupersededDate ?? now;
        entry.ExternalDocumentStatus = ExternalDocumentStatus.Superseded;
        if (!string.IsNullOrWhiteSpace(input.SupersessionSummary))
        {
            entry.LastKnownChangeSummary = input.SupersessionSummary.Trim();
        }

        // SOP §10.3 — internal documents that depend on a superseded source must be assessed.
        if (await HasOpenLinksAsync(entry.Id, ct))
        {
            entry.ExternalDocumentStatus = ExternalDocumentStatus.ActionRequired;
            entry.RequiresImpactAssessment = true;
            await RaiseImpactAssessmentAsync(entry, ExternalImpactTriggerType.Supersession, now, ct);
            await FlagLinksAsync(entry.Id, ExternalDocumentLinkStatus.ActionRequired, now, ct);
        }

        Touch(entry, now);
        await _externalDocuments.UpdateAsync(entry, ct);
        return Response<ExternalDocumentModel>.Success(ExternalDocumentWire.ToModel(entry, now), correlationId: correlationId);
    }

    /// <summary>Archival is a STATUS change — the register row and its evidence trail are never deleted.</summary>
    public async Task<Response<ExternalDocumentModel>> ArchiveAsync(Guid id, ArchiveExternalDocumentInput input, string correlationId, CancellationToken ct)
    {
        var (fail, entry) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("An archive reason is required.", 400, ExternalDocumentReasonCodes.ValidationFailed, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        entry!.ExternalDocumentStatus = ExternalDocumentStatus.Archived;
        entry.LastKnownChangeSummary = input.Reason.Trim();
        entry.NextCheckDueDate = null; // an archived source is no longer monitored
        Touch(entry, now);
        await _externalDocuments.UpdateAsync(entry, ct);
        return Response<ExternalDocumentModel>.Success(ExternalDocumentWire.ToModel(entry, now), correlationId: correlationId);
    }

    // ── monitoring ────────────────────────────────────────────────────────────

    public async Task<Response<ExternalDocumentMonitoringCheckModel>> RecordMonitoringCheckAsync(
        Guid id, RecordMonitoringCheckInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _externalDocuments.GetByIdAsync(id, ct);
        if (entry is null)
        {
            return Response<ExternalDocumentMonitoringCheckModel>.Fail(
                "External document not found.", 404, ExternalDocumentReasonCodes.ExternalDocumentNotFound, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.MonitoringSource))
        {
            return FailCheck("A monitoring source is required.", 400, ExternalDocumentReasonCodes.MonitoringSourceRequired, correlationId);
        }

        // SOP §10.2 — a monitoring check without evidence is not a check.
        if (string.IsNullOrWhiteSpace(input.EvidenceReference))
        {
            return FailCheck("An evidence reference is required to record a monitoring check.", 400, ExternalDocumentReasonCodes.EvidenceReferenceRequired, correlationId);
        }

        if (input.ChangeDetected && string.IsNullOrWhiteSpace(input.ChangeSummary))
        {
            return FailCheck("A change summary is required when a change is detected.", 400, ExternalDocumentReasonCodes.ChangeSummaryRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var checkDate = input.CheckDate ?? now;
        var nextDue = ExternalDocumentScheduleCalculator.NextCheckDueDate(entry.MonitoringFrequency, checkDate);

        var check = new ExternalDocumentMonitoringCheck
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            ExternalDocumentRegisterEntryId = entry.Id,
            CheckDate = checkDate,
            CheckedBy = _currentUser.ActorName,
            CheckedByUserId = _currentUser.UserId,
            MonitoringSource = input.MonitoringSource.Trim(),
            SourceVersionObserved = Trim(input.SourceVersionObserved),
            SourceEffectiveDateObserved = input.SourceEffectiveDateObserved,
            ChangeDetected = input.ChangeDetected,
            ChangeSummary = Trim(input.ChangeSummary),
            EvidenceReference = input.EvidenceReference.Trim(),
            NextCheckDueDate = nextDue,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _checks.CreateAsync(check, ct);

        entry.LastCheckedAt = checkDate;
        entry.LastCheckedBy = _currentUser.ActorName;
        entry.NextCheckDueDate = nextDue;

        if (input.ChangeDetected)
        {
            // SOP §10.3 — a detected source change raises an impact assessment requirement.
            entry.LastKnownChangeSummary = Trim(input.ChangeSummary);
            entry.RequiresImpactAssessment = true;
            entry.ExternalDocumentStatus = ExternalDocumentStatus.ActionRequired;
            await RaiseImpactAssessmentAsync(entry, ExternalImpactTriggerType.VersionChange, now, ct);
        }
        else if (entry.ExternalDocumentStatus == ExternalDocumentStatus.Active)
        {
            entry.ExternalDocumentStatus = ExternalDocumentStatus.Monitoring;
        }

        Touch(entry, now);
        await _externalDocuments.UpdateAsync(entry, ct);
        return Response<ExternalDocumentMonitoringCheckModel>.Success(ExternalDocumentWire.ToCheck(check), 201, correlationId);
    }

    public async Task<Response<IReadOnlyList<ExternalDocumentMonitoringCheckModel>>> GetMonitoringChecksAsync(Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _externalDocuments.GetByIdAsync(id, ct);
        if (entry is null)
        {
            return Response<IReadOnlyList<ExternalDocumentMonitoringCheckModel>>.Fail(
                "External document not found.", 404, ExternalDocumentReasonCodes.ExternalDocumentNotFound, correlationId);
        }

        var rows = await _checks.GetByExternalDocumentAsync(id, ct);
        return Response<IReadOnlyList<ExternalDocumentMonitoringCheckModel>>.Success(
            rows.Select(ExternalDocumentWire.ToCheck).ToList(), correlationId: correlationId);
    }

    /// <summary>SOP §10.2 — sources whose monitoring is past due (or never checked and already past due).</summary>
    public async Task<Response<IReadOnlyList<ExternalDocumentMonitoringDueModel>>> GetMonitoringDueAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var now = DateTimeOffset.UtcNow;
        var rows = await _externalDocuments.GetAllForTenantAsync(ct);
        var due = rows
            .Where(x => ExternalDocumentWire.IsMonitoringOverdue(x, now))
            .OrderBy(x => x.NextCheckDueDate)
            .Select(x => new ExternalDocumentMonitoringDueModel(
                x.Id, x.ExternalDocumentTitle, x.ExternalAuthorityName, x.MonitoringFrequency.ToString(),
                x.MonitoringOwnerUserId, x.MonitoringOwnerRole, x.LastCheckedAt, x.NextCheckDueDate,
                x.NextCheckDueDate is { } d ? (int)Math.Floor((now - d).TotalDays) : 0,
                x.LastCheckedAt is null))
            .ToList();

        return Response<IReadOnlyList<ExternalDocumentMonitoringDueModel>>.Success(due, correlationId: correlationId);
    }

    // ── impact assessment ─────────────────────────────────────────────────────

    public async Task<Response<ExternalDocumentImpactAssessmentModel>> CreateImpactAssessmentAsync(
        Guid id, CreateExternalImpactAssessmentInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _externalDocuments.GetByIdAsync(id, ct);
        if (entry is null)
        {
            return Response<ExternalDocumentImpactAssessmentModel>.Fail(
                "External document not found.", 404, ExternalDocumentReasonCodes.ExternalDocumentNotFound, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var triggerDate = input.TriggerDate ?? now;
        var regulated = ExternalDocumentScheduleCalculator.HasRegulatedImpact(
            input.HasGmpImpact, input.HasGdpImpact, input.HasPvImpact, input.HasRaImpact);

        var assessment = new ExternalDocumentImpactAssessment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            ExternalDocumentRegisterEntryId = entry.Id,
            AssessmentStatus = ExternalImpactAssessmentStatus.Pending,
            TriggerType = ExternalDocumentWire.ParseTrigger(input.TriggerType),
            DueDate = ExternalDocumentScheduleCalculator.ImpactAssessmentDueDate(triggerDate, regulated),
            HasGmpImpact = input.HasGmpImpact,
            HasGdpImpact = input.HasGdpImpact,
            HasPvImpact = input.HasPvImpact,
            HasRaImpact = input.HasRaImpact,
            HasBatchReleaseImpact = input.HasBatchReleaseImpact,
            HasTrainingImpact = input.HasTrainingImpact,
            HasDocumentImpact = input.HasDocumentImpact,
            ImpactSummary = Trim(input.ImpactSummary),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _assessments.CreateAsync(assessment, ct);

        entry.RequiresImpactAssessment = true;
        entry.ImpactAssessmentStatus = ExternalImpactAssessmentStatus.Pending;
        entry.ImpactAssessmentDueDate = assessment.DueDate;
        entry.HasGmpImpact |= input.HasGmpImpact;
        entry.HasGdpImpact |= input.HasGdpImpact;
        entry.HasPvImpact |= input.HasPvImpact;
        entry.HasRaImpact |= input.HasRaImpact;
        entry.HasBatchReleaseImpact |= input.HasBatchReleaseImpact;
        entry.HasTrainingImpact |= input.HasTrainingImpact;
        entry.HasDocumentImpact |= input.HasDocumentImpact;
        Touch(entry, now);
        await _externalDocuments.UpdateAsync(entry, ct);

        return Response<ExternalDocumentImpactAssessmentModel>.Success(
            ExternalDocumentWire.ToAssessment(assessment, now), 201, correlationId);
    }

    public async Task<Response<ExternalDocumentImpactAssessmentModel>> CompleteImpactAssessmentAsync(
        Guid id, Guid assessmentId, CompleteExternalImpactAssessmentInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _externalDocuments.GetByIdAsync(id, ct);
        if (entry is null)
        {
            return Response<ExternalDocumentImpactAssessmentModel>.Fail(
                "External document not found.", 404, ExternalDocumentReasonCodes.ExternalDocumentNotFound, correlationId);
        }

        var assessment = await _assessments.GetByIdAsync(assessmentId, ct);
        if (assessment is null || assessment.ExternalDocumentRegisterEntryId != entry.Id)
        {
            return Response<ExternalDocumentImpactAssessmentModel>.Fail(
                "Impact assessment not found.", 404, ExternalDocumentReasonCodes.AssessmentNotFound, correlationId);
        }

        if (assessment.AssessmentStatus == ExternalImpactAssessmentStatus.Completed)
        {
            return Response<ExternalDocumentImpactAssessmentModel>.Fail(
                "The impact assessment is already completed.", 409, ExternalDocumentReasonCodes.AlreadyCompleted, correlationId);
        }

        // SOP §10.3 — an assessment cannot be closed without evidence.
        if (string.IsNullOrWhiteSpace(input.AssessmentEvidenceReference))
        {
            return Response<ExternalDocumentImpactAssessmentModel>.Fail(
                "Assessment evidence is required to complete an impact assessment.", 400,
                ExternalDocumentReasonCodes.AssessmentEvidenceRequired, correlationId);
        }

        // A document impact must land somewhere traceable: a linked internal document or an action reference.
        if (assessment.HasDocumentImpact)
        {
            var links = await _links.GetByExternalDocumentAsync(entry.Id, ct);
            var hasLink = links.Any(l => l.LinkStatus != ExternalDocumentLinkStatus.Closed);
            if (!hasLink && string.IsNullOrWhiteSpace(input.ActionReference))
            {
                return Response<ExternalDocumentImpactAssessmentModel>.Fail(
                    "A document impact requires either a linked internal register entry or an action reference.", 409,
                    ExternalDocumentReasonCodes.DocumentImpactActionRequired, correlationId);
            }
        }

        var now = DateTimeOffset.UtcNow;
        assessment.AssessmentStatus = ExternalImpactAssessmentStatus.Completed;
        assessment.AssessmentEvidenceReference = input.AssessmentEvidenceReference.Trim();
        assessment.RecommendedAction = ExternalDocumentWire.ParseAction(input.RecommendedAction);
        assessment.ImpactSummary = Trim(input.ImpactSummary) ?? assessment.ImpactSummary;
        assessment.CompletedAt = now;
        assessment.CompletedBy = _currentUser.ActorName;
        assessment.CompletedByUserId = _currentUser.UserId;
        assessment.StartedAt ??= assessment.CreatedAt;
        assessment.ActionOwnerUserId = input.ActionOwnerUserId;
        assessment.ActionOwnerRole = Trim(input.ActionOwnerRole);
        assessment.ActionDueDate = input.ActionDueDate;
        assessment.ActionReference = Trim(input.ActionReference);
        assessment.UpdatedAt = now;
        assessment.UpdatedBy = _currentUser.ActorName;
        await _assessments.UpdateAsync(assessment, ct);

        // NOTE: RecommendedAction is a RECOMMENDATION ONLY. Revise/Suspend/Retire deliberately do NOT touch the
        // internal document — the FU08 lifecycle engine and FU13 suspension engine remain the only paths.
        await RefreshImpactRollupAsync(entry, now, ct);
        return Response<ExternalDocumentImpactAssessmentModel>.Success(
            ExternalDocumentWire.ToAssessment(assessment, now), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<ExternalDocumentImpactAssessmentModel>>> GetImpactAssessmentsAsync(Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _externalDocuments.GetByIdAsync(id, ct);
        if (entry is null)
        {
            return Response<IReadOnlyList<ExternalDocumentImpactAssessmentModel>>.Fail(
                "External document not found.", 404, ExternalDocumentReasonCodes.ExternalDocumentNotFound, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var rows = await _assessments.GetByExternalDocumentAsync(id, ct);
        return Response<IReadOnlyList<ExternalDocumentImpactAssessmentModel>>.Success(
            rows.Select(x => ExternalDocumentWire.ToAssessment(x, now)).ToList(), correlationId: correlationId);
    }

    /// <summary>SOP §10.3 — assessments past their due date. Overdue is PERSISTED so the register reflects it.</summary>
    public async Task<Response<IReadOnlyList<ExternalDocumentImpactAssessmentModel>>> GetOverdueImpactAssessmentsAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var now = DateTimeOffset.UtcNow;
        var rows = await _assessments.GetAllForTenantAsync(ct);
        var overdue = rows
            .Where(x => x.AssessmentStatus is not (ExternalImpactAssessmentStatus.Completed or ExternalImpactAssessmentStatus.NotRequired)
                        && now > x.DueDate)
            .OrderBy(x => x.DueDate)
            .ToList();

        foreach (var assessment in overdue.Where(x => x.AssessmentStatus != ExternalImpactAssessmentStatus.Overdue))
        {
            assessment.AssessmentStatus = ExternalImpactAssessmentStatus.Overdue;
            assessment.UpdatedAt = now;
            assessment.UpdatedBy = _currentUser.ActorName;
            await _assessments.UpdateAsync(assessment, ct);

            var entry = await _externalDocuments.GetByIdAsync(assessment.ExternalDocumentRegisterEntryId, ct);
            if (entry is not null)
            {
                entry.ImpactAssessmentStatus = ExternalImpactAssessmentStatus.Overdue;
                entry.ExternalDocumentStatus = ExternalDocumentStatus.ActionRequired;
                Touch(entry, now);
                await _externalDocuments.UpdateAsync(entry, ct);
            }
        }

        return Response<IReadOnlyList<ExternalDocumentImpactAssessmentModel>>.Success(
            overdue.Select(x => ExternalDocumentWire.ToAssessment(x, now)).ToList(), correlationId: correlationId);
    }

    // ── internal register links ───────────────────────────────────────────────

    public async Task<Response<ExternalDocumentInternalLinkModel>> LinkToInternalRegisterAsync(
        Guid id, LinkExternalDocumentToInternalInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _externalDocuments.GetByIdAsync(id, ct);
        if (entry is null)
        {
            return Response<ExternalDocumentInternalLinkModel>.Fail(
                "External document not found.", 404, ExternalDocumentReasonCodes.ExternalDocumentNotFound, correlationId);
        }

        // Tenant-scoped lookup: a foreign-tenant internal entry simply does not resolve (no existence leakage).
        var internalEntry = await _internalRegister.GetByIdAsync(input.InternalRegisterEntryId, ct);
        if (internalEntry is null)
        {
            return Response<ExternalDocumentInternalLinkModel>.Fail(
                "Internal register entry not found.", 404, ExternalDocumentReasonCodes.InternalEntryNotFound, correlationId);
        }

        // An external document is never linked to another external row masquerading as an internal one.
        if (internalEntry.IsExternalDocument)
        {
            return Response<ExternalDocumentInternalLinkModel>.Fail(
                "The target register entry is itself flagged as an external document and cannot be linked as an internal document.",
                409, ExternalDocumentReasonCodes.InternalEntryIsExternal, correlationId);
        }

        var linkType = ExternalDocumentWire.ParseLinkType(input.LinkType);
        var existing = (await _links.GetByExternalDocumentAsync(entry.Id, ct))
            .FirstOrDefault(l => l.InternalRegisterEntryId == internalEntry.Id && l.LinkType == linkType);

        // Idempotent: re-linking the same pair returns the existing link instead of duplicating it.
        if (existing is not null)
        {
            return Response<ExternalDocumentInternalLinkModel>.Success(ExternalDocumentWire.ToLink(existing), correlationId: correlationId);
        }

        var link = new ExternalDocumentInternalLink
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            ExternalDocumentRegisterEntryId = entry.Id,
            InternalRegisterEntryId = internalEntry.Id,
            LinkType = linkType,
            LinkStatus = ExternalDocumentLinkStatus.Active,
            Notes = Trim(input.Notes),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _links.CreateAsync(link, ct);
        return Response<ExternalDocumentInternalLinkModel>.Success(ExternalDocumentWire.ToLink(link), 201, correlationId);
    }

    public async Task<Response<IReadOnlyList<ExternalDocumentInternalLinkModel>>> GetInternalLinksAsync(Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _externalDocuments.GetByIdAsync(id, ct);
        if (entry is null)
        {
            return Response<IReadOnlyList<ExternalDocumentInternalLinkModel>>.Fail(
                "External document not found.", 404, ExternalDocumentReasonCodes.ExternalDocumentNotFound, correlationId);
        }

        var rows = await _links.GetByExternalDocumentAsync(id, ct);
        return Response<IReadOnlyList<ExternalDocumentInternalLinkModel>>.Success(
            rows.Select(ExternalDocumentWire.ToLink).ToList(), correlationId: correlationId);
    }

    /// <summary>Closing a link is a STATUS change — links are never hard-deleted.</summary>
    public async Task<Response<ExternalDocumentInternalLinkModel>> CloseInternalLinkAsync(Guid id, Guid linkId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var link = (await _links.GetByExternalDocumentAsync(id, ct)).FirstOrDefault(l => l.Id == linkId);
        if (link is null)
        {
            return Response<ExternalDocumentInternalLinkModel>.Fail(
                "Link not found.", 404, ExternalDocumentReasonCodes.NotFoundNonLeakage, correlationId);
        }

        link.LinkStatus = ExternalDocumentLinkStatus.Closed;
        link.UpdatedAt = DateTimeOffset.UtcNow;
        link.UpdatedBy = _currentUser.ActorName;
        await _links.UpdateAsync(link, ct);
        return Response<ExternalDocumentInternalLinkModel>.Success(ExternalDocumentWire.ToLink(link), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private sealed record ValidationFailure(string Message, string ReasonCode);

    private static ValidationFailure? Validate(ExternalDocumentFieldsInput i)
    {
        if (string.IsNullOrWhiteSpace(i.ExternalDocumentTitle))
        {
            return new ValidationFailure("An external document title is required.", ExternalDocumentReasonCodes.TitleRequired);
        }

        if (string.IsNullOrWhiteSpace(i.ExternalAuthorityName))
        {
            return new ValidationFailure("An issuing authority is required.", ExternalDocumentReasonCodes.AuthorityRequired);
        }

        if (string.IsNullOrWhiteSpace(i.SourceReference))
        {
            return new ValidationFailure("A source reference is required.", ExternalDocumentReasonCodes.SourceReferenceRequired);
        }

        // SOP §10.2 — an external document must have a NAMED monitoring owner (a user or an accountable role).
        if ((i.MonitoringOwnerUserId is null || i.MonitoringOwnerUserId == Guid.Empty)
            && string.IsNullOrWhiteSpace(i.MonitoringOwnerRole))
        {
            return new ValidationFailure("A named monitoring owner (user or role) is required.", ExternalDocumentReasonCodes.MonitoringOwnerRequired);
        }

        if (ExternalDocumentWire.ParseFrequency(i.MonitoringFrequency) is null)
        {
            return new ValidationFailure("A valid monitoring frequency is required.", ExternalDocumentReasonCodes.MonitoringFrequencyRequired);
        }

        if (ExternalDocumentWire.ParseSourceStatus(i.SourceStatus) is null)
        {
            return new ValidationFailure("A valid source status is required.", ExternalDocumentReasonCodes.SourceStatusRequired);
        }

        return null;
    }

    private static void ApplyFields(ExternalDocumentRegisterEntry e, ExternalDocumentFieldsInput i)
    {
        e.ExternalDocumentCode = Trim(i.ExternalDocumentCode);
        e.ExternalDocumentType = ExternalDocumentWire.ParseType(i.ExternalDocumentType);
        e.Jurisdiction = Trim(i.Jurisdiction);
        e.CountryCode = Trim(i.CountryCode);
        e.RegionCode = Trim(i.RegionCode);
        e.SourceUrl = Trim(i.SourceUrl); // reference only — never fetched
        e.SourceVersion = Trim(i.SourceVersion);
        e.SourceEffectiveDate = i.SourceEffectiveDate;
        e.SourcePublishedDate = i.SourcePublishedDate;
        e.SourceSupersededDate = i.SourceSupersededDate;
        e.SourceStatus = ExternalDocumentWire.ParseSourceStatus(i.SourceStatus)!.Value;
        e.MonitoringOwnerUserId = i.MonitoringOwnerUserId == Guid.Empty ? null : i.MonitoringOwnerUserId;
        e.MonitoringOwnerRole = Trim(i.MonitoringOwnerRole);
        e.MonitoringFunction = Trim(i.MonitoringFunction);
        e.HasGmpImpact = i.HasGmpImpact;
        e.HasGdpImpact = i.HasGdpImpact;
        e.HasPvImpact = i.HasPvImpact;
        e.HasRaImpact = i.HasRaImpact;
        e.HasBatchReleaseImpact = i.HasBatchReleaseImpact;
        e.HasTrainingImpact = i.HasTrainingImpact;
        e.HasDocumentImpact = i.HasDocumentImpact;
    }

    /// <summary>
    /// A regulated impact flagged on the entry itself makes an assessment mandatory with the 10-working-day clock.
    /// A DraftConsultation source never becomes mandatory — SOP §10.4 keeps it as regulatory intelligence.
    /// </summary>
    private static void ApplyImpactRollup(ExternalDocumentRegisterEntry e, DateTimeOffset now)
    {
        if (e.ImpactAssessmentStatus is ExternalImpactAssessmentStatus.Pending or ExternalImpactAssessmentStatus.InProgress
            or ExternalImpactAssessmentStatus.Overdue or ExternalImpactAssessmentStatus.Blocked)
        {
            return; // an assessment is already running; do not reset its state from a metadata edit
        }

        var regulated = ExternalDocumentScheduleCalculator.HasRegulatedImpact(e.HasGmpImpact, e.HasGdpImpact, e.HasPvImpact, e.HasRaImpact);
        if (regulated && e.SourceStatus != ExternalSourceStatus.DraftConsultation)
        {
            e.RequiresImpactAssessment = true;
            e.ImpactAssessmentDueDate ??= ExternalDocumentScheduleCalculator.ImpactAssessmentDueDate(now, hasRegulatedImpact: true);
            if (e.ImpactAssessmentStatus == ExternalImpactAssessmentStatus.NotRequired)
            {
                e.ImpactAssessmentStatus = ExternalImpactAssessmentStatus.Pending;
            }
        }
    }

    /// <summary>Raises a Pending assessment unless one is already open for this entry (never duplicates).</summary>
    private async Task RaiseImpactAssessmentAsync(
        ExternalDocumentRegisterEntry entry, ExternalImpactTriggerType trigger, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await _assessments.GetByExternalDocumentAsync(entry.Id, ct);
        if (existing.Any(a => a.AssessmentStatus is ExternalImpactAssessmentStatus.Pending
                or ExternalImpactAssessmentStatus.InProgress or ExternalImpactAssessmentStatus.Overdue))
        {
            entry.RequiresImpactAssessment = true;
            return;
        }

        var regulated = ExternalDocumentScheduleCalculator.HasRegulatedImpact(
            entry.HasGmpImpact, entry.HasGdpImpact, entry.HasPvImpact, entry.HasRaImpact);
        var assessment = new ExternalDocumentImpactAssessment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            ExternalDocumentRegisterEntryId = entry.Id,
            AssessmentStatus = ExternalImpactAssessmentStatus.Pending,
            TriggerType = trigger,
            DueDate = ExternalDocumentScheduleCalculator.ImpactAssessmentDueDate(now, regulated),
            HasGmpImpact = entry.HasGmpImpact,
            HasGdpImpact = entry.HasGdpImpact,
            HasPvImpact = entry.HasPvImpact,
            HasRaImpact = entry.HasRaImpact,
            HasBatchReleaseImpact = entry.HasBatchReleaseImpact,
            HasTrainingImpact = entry.HasTrainingImpact,
            HasDocumentImpact = entry.HasDocumentImpact,
            ImpactSummary = entry.LastKnownChangeSummary,
            CorrelationId = entry.CorrelationId,
            CreatedBy = _currentUser.ActorName
        };
        await _assessments.CreateAsync(assessment, ct);

        entry.RequiresImpactAssessment = true;
        entry.ImpactAssessmentStatus = ExternalImpactAssessmentStatus.Pending;
        entry.ImpactAssessmentDueDate = assessment.DueDate;
    }

    /// <summary>After a completion, the entry's rollup reflects whatever assessments remain open.</summary>
    private async Task RefreshImpactRollupAsync(ExternalDocumentRegisterEntry entry, DateTimeOffset now, CancellationToken ct)
    {
        var all = await _assessments.GetByExternalDocumentAsync(entry.Id, ct);
        var open = all.Where(a => a.AssessmentStatus is ExternalImpactAssessmentStatus.Pending
            or ExternalImpactAssessmentStatus.InProgress or ExternalImpactAssessmentStatus.Overdue
            or ExternalImpactAssessmentStatus.Blocked).ToList();

        if (open.Count == 0)
        {
            entry.RequiresImpactAssessment = false;
            entry.ImpactAssessmentStatus = ExternalImpactAssessmentStatus.Completed;
            entry.ImpactAssessmentDueDate = null;
            if (entry.ExternalDocumentStatus == ExternalDocumentStatus.ActionRequired)
            {
                entry.ExternalDocumentStatus = ExternalDocumentStatus.Monitoring;
            }
        }
        else
        {
            entry.RequiresImpactAssessment = true;
            entry.ImpactAssessmentStatus = open.Any(a => a.AssessmentStatus == ExternalImpactAssessmentStatus.Overdue)
                ? ExternalImpactAssessmentStatus.Overdue
                : ExternalImpactAssessmentStatus.Pending;
            entry.ImpactAssessmentDueDate = open.Min(a => a.DueDate);
        }

        Touch(entry, now);
        await _externalDocuments.UpdateAsync(entry, ct);
    }

    private async Task<bool> HasOpenLinksAsync(Guid externalDocumentId, CancellationToken ct) =>
        (await _links.GetByExternalDocumentAsync(externalDocumentId, ct))
        .Any(l => l.LinkStatus != ExternalDocumentLinkStatus.Closed);

    private async Task FlagLinksAsync(Guid externalDocumentId, ExternalDocumentLinkStatus status, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var link in (await _links.GetByExternalDocumentAsync(externalDocumentId, ct))
                 .Where(l => l.LinkStatus != ExternalDocumentLinkStatus.Closed && l.LinkStatus != status))
        {
            link.LinkStatus = status;
            link.UpdatedAt = now;
            link.UpdatedBy = _currentUser.ActorName;
            await _links.UpdateAsync(link, ct);
        }
    }

    private async Task<(Response<ExternalDocumentModel>? Fail, ExternalDocumentRegisterEntry? Entry)> LoadAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _externalDocuments.GetByIdAsync(id, ct);
        return entry is null
            ? (Fail("External document not found.", 404, ExternalDocumentReasonCodes.ExternalDocumentNotFound, correlationId), null)
            : (null, entry);
    }

    private void Touch(ExternalDocumentRegisterEntry entry, DateTimeOffset now)
    {
        entry.UpdatedAt = now;
        entry.UpdatedBy = _currentUser.ActorName;
    }

    private static Response<ExternalDocumentModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<ExternalDocumentModel>.Fail(error, status, reason, correlationId);

    private static Response<ExternalDocumentMonitoringCheckModel> FailCheck(string error, int status, string reason, string correlationId) =>
        Response<ExternalDocumentMonitoringCheckModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
