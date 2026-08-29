using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementControlledCopy.Services;

/// <summary>
/// MOD-0029-FU17 — Controlled Copy Log + obsolete copy reconciliation orchestration (GMG-QMS-SOP-0001 §9.13, §18
/// LOG-0002, §19 gate 6). Registers controlled copies, drives withdrawal/reconciliation, raises obsolete-copy findings
/// (use of an obsolete document is a QA quality event — captured as a REFERENCE, no CAPA module here), and computes the
/// withdrawal readiness that FU10 Gate 6 consumes. No hard delete; the copy log is permanent.
/// </summary>
public sealed class DocumentControlledCopyService
{
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IDocumentControlledCopyRepository _copies;
    private readonly IDocumentCopyWithdrawalPlanRepository _plans;
    private readonly IDocumentObsoleteCopyFindingRepository _findings;
    private readonly DocumentControlledCopyReadinessEvaluator _readiness;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentControlledCopyService(
        IDocumentMasterRegisterRepository register,
        IDocumentControlledCopyRepository copies,
        IDocumentCopyWithdrawalPlanRepository plans,
        IDocumentObsoleteCopyFindingRepository findings,
        DocumentControlledCopyReadinessEvaluator readiness,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _register = register;
        _copies = copies;
        _plans = plans;
        _findings = findings;
        _readiness = readiness;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    // ── controlled copies ─────────────────────────────────────────────────────

    public async Task<Response<ControlledCopyModel>> RegisterCopyAsync(Guid registerEntryId, RegisterControlledCopyInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return FailCopy("Register entry not found.", 404, ControlledCopyReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var type = ControlledCopyWire.ParseType(input.CopyType);
        if (type is null)
        {
            return FailCopy("A valid copy type is required.", 400, ControlledCopyReasonCodes.ValidationFailed, correlationId);
        }

        // An ACTIVE controlled copy may only exist for a document in force (SOP §9.13).
        if (!entry.LifecycleStatus.IsOperationallyEffective())
        {
            return FailCopy($"A {entry.LifecycleStatus} document cannot have a new active controlled copy.", 409, ControlledCopyReasonCodes.NotEligibleForActiveCopy, correlationId);
        }

        // Printed / external copies must record who holds them and where (SOP §18 LOG-0002).
        if (type is ControlledCopyType.PrintedControlledCopy or ControlledCopyType.ExternalSharedCopy
            && string.IsNullOrWhiteSpace(input.LocationDescription) && input.HolderUserId is null && string.IsNullOrWhiteSpace(input.HolderRole) && string.IsNullOrWhiteSpace(input.HolderDepartment))
        {
            return FailCopy("A printed/external controlled copy requires a holder or a location.", 400, ControlledCopyReasonCodes.HolderOrLocationRequired, correlationId);
        }

        var existing = await _copies.GetByRegisterEntryAsync(registerEntryId, ct);
        var copyNumber = input.CopyNumber is > 0 ? input.CopyNumber.Value : existing.Select(x => x.CopyNumber).DefaultIfEmpty(0).Max() + 1;
        if (input.CopyNumber is > 0 && existing.Any(x => x.CopyNumber == copyNumber))
        {
            return FailCopy("A controlled copy with this number already exists for the document.", 409, ControlledCopyReasonCodes.DuplicateCopyNumber, correlationId);
        }

        var copy = new DocumentControlledCopy
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = registerEntryId,
            ControlledDocumentId = input.ControlledDocumentId,
            ControlledDocumentVersionId = input.ControlledDocumentVersionId,
            CopyNumber = copyNumber,
            CopyType = type.Value,
            CopyStatus = ControlledCopyStatus.Active,
            LocationType = ControlledCopyWire.ParseLocation(input.LocationType),
            LocationDescription = Trim(input.LocationDescription),
            HolderUserId = input.HolderUserId,
            HolderRole = Trim(input.HolderRole),
            HolderDepartment = Trim(input.HolderDepartment),
            RepositoryAssessmentId = input.RepositoryAssessmentId,
            IssuedAt = DateTimeOffset.UtcNow,
            IssuedBy = _currentUser.ActorName,
            EffectiveFrom = entry.EffectiveDate,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _copies.CreateAsync(copy, ct);
        return Response<ControlledCopyModel>.Success(ControlledCopyWire.ToCopy(copy), 201, correlationId);
    }

    public async Task<Response<ControlledCopyModel>> WithdrawAsync(Guid registerEntryId, Guid copyId, WithdrawControlledCopyInput input, string correlationId, CancellationToken ct)
    {
        var (fail, copy) = await LoadCopyAsync(registerEntryId, copyId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.WithdrawalEvidenceReference))
        {
            return FailCopy("Withdrawal evidence is required.", 400, ControlledCopyReasonCodes.EvidenceRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        copy!.CopyStatus = ControlledCopyStatus.Withdrawn;
        copy.WithdrawnAt = now;
        copy.WithdrawnBy = _currentUser.ActorName;
        copy.WithdrawalEvidenceReference = input.WithdrawalEvidenceReference.Trim();
        Touch(copy);
        await _copies.UpdateAsync(copy, ct);
        await ResolveCopyFindingsAsync(copy.Id, ct);
        return Response<ControlledCopyModel>.Success(ControlledCopyWire.ToCopy(copy), correlationId: correlationId);
    }

    public async Task<Response<ControlledCopyModel>> ReconcileAsync(Guid registerEntryId, Guid copyId, ReconcileControlledCopyInput input, string correlationId, CancellationToken ct)
    {
        var (fail, copy) = await LoadCopyAsync(registerEntryId, copyId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.ReconciliationEvidenceReference))
        {
            return FailCopy("Reconciliation evidence is required.", 400, ControlledCopyReasonCodes.EvidenceRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        copy!.CopyStatus = ControlledCopyStatus.Reconciled;
        copy.ReconciledAt = now;
        copy.ReconciledBy = _currentUser.ActorName;
        copy.ReconciliationEvidenceReference = input.ReconciliationEvidenceReference.Trim();
        Touch(copy);
        await _copies.UpdateAsync(copy, ct);
        await ResolveCopyFindingsAsync(copy.Id, ct);
        return Response<ControlledCopyModel>.Success(ControlledCopyWire.ToCopy(copy), correlationId: correlationId);
    }

    public async Task<Response<ControlledCopyModel>> MarkMissingAsync(Guid registerEntryId, Guid copyId, MarkControlledCopyMissingInput input, string correlationId, CancellationToken ct)
    {
        var (fail, copy) = await LoadCopyAsync(registerEntryId, copyId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        copy!.CopyStatus = ControlledCopyStatus.Missing;
        copy.WithdrawalRequired = true;
        copy.Comment = Trim(input.Comment);
        Touch(copy);
        await _copies.UpdateAsync(copy, ct);

        await RaiseFindingAsync(copy.RegisterEntryId, copy.Id, ObsoleteCopyFindingType.MissingCopyDuringReconciliation,
            ObsoleteCopyFindingSeverity.Major, "A controlled copy is missing during reconciliation.", copy.LocationDescription, correlationId, ct);
        return Response<ControlledCopyModel>.Success(ControlledCopyWire.ToCopy(copy), correlationId: correlationId);
    }

    public async Task<Response<ControlledCopyModel>> MarkObsoleteAsync(Guid registerEntryId, Guid copyId, MarkControlledCopyObsoleteInput input, string correlationId, CancellationToken ct)
    {
        var (fail, copy) = await LoadCopyAsync(registerEntryId, copyId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.ObsoleteReason))
        {
            return FailCopy("An obsolete reason is required.", 400, ControlledCopyReasonCodes.ReasonRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        copy!.CopyStatus = ControlledCopyStatus.Obsolete;
        copy.WithdrawalRequired = true;
        copy.ObsoleteDetectedAt = now;
        copy.ObsoleteDetectedBy = _currentUser.ActorName;
        copy.ObsoleteReason = input.ObsoleteReason.Trim();
        Touch(copy);
        await _copies.UpdateAsync(copy, ct);

        // Use of an obsolete/uncontrolled copy is a QA quality event (SOP §16) — captured as a critical finding.
        await RaiseFindingAsync(copy.RegisterEntryId, copy.Id, ObsoleteCopyFindingType.UncontrolledCopyDetected,
            ObsoleteCopyFindingSeverity.Critical, $"Obsolete copy detected: {copy.ObsoleteReason}",
            Trim(input.LocationDescription) ?? copy.LocationDescription, correlationId, ct);
        return Response<ControlledCopyModel>.Success(ControlledCopyWire.ToCopy(copy), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<ControlledCopyModel>>> ListCopiesAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<IReadOnlyList<ControlledCopyModel>>.Fail("Register entry not found.", 404, ControlledCopyReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var rows = await _copies.GetByRegisterEntryAsync(registerEntryId, ct);
        return Response<IReadOnlyList<ControlledCopyModel>>.Success(rows.Select(ControlledCopyWire.ToCopy).ToList(), correlationId: correlationId);
    }

    // ── withdrawal plan ───────────────────────────────────────────────────────

    public async Task<Response<WithdrawalPlanModel>> GeneratePlanAsync(Guid registerEntryId, GenerateWithdrawalPlanInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return FailPlan("Register entry not found.", 404, ControlledCopyReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var plan = await GenerateCoreAsync(entry, ControlledCopyWire.ParseTrigger(input.TriggerType), input.DueDate, correlationId, ct);
        return Response<WithdrawalPlanModel>.Success(ControlledCopyWire.ToPlan(plan), 201, correlationId);
    }

    /// <summary>Shared core used by the command and the FU13 withdrawal port adapter. Idempotent per open plan.</summary>
    public async Task<DocumentCopyWithdrawalPlan> GenerateCoreAsync(DocumentMasterRegisterEntry entry, CopyWithdrawalTriggerType trigger, DateTimeOffset? dueDate, string correlationId, CancellationToken ct)
    {
        var open = await _plans.GetOpenAsync(entry.Id, ct);
        if (open is not null)
        {
            return open;
        }

        var copies = await _copies.GetByRegisterEntryAsync(entry.Id, ct);
        var toWithdraw = copies.Where(c => c.CopyStatus == ControlledCopyStatus.Active).ToList();

        foreach (var copy in toWithdraw)
        {
            copy.CopyStatus = ControlledCopyStatus.PendingWithdrawal;
            copy.WithdrawalRequired = true;
            copy.WithdrawalDueDate = dueDate;
            Touch(copy);
            await _copies.UpdateAsync(copy, ct);
        }

        var plan = new DocumentCopyWithdrawalPlan
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = entry.Id,
            TriggerType = trigger,
            PlanStatus = toWithdraw.Count == 0 ? CopyWithdrawalPlanStatus.Completed : CopyWithdrawalPlanStatus.Active,
            RequiredCopyCount = toWithdraw.Count,
            DueDate = dueDate,
            CompletedAt = toWithdraw.Count == 0 ? DateTimeOffset.UtcNow : null,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _plans.CreateAsync(plan, ct);
        return plan;
    }

    public async Task<Response<WithdrawalPlanModel>> CompletePlanAsync(Guid registerEntryId, Guid planId, CompleteWithdrawalPlanInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return FailPlan("Register entry not found.", 404, ControlledCopyReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var plan = await _plans.GetByIdAsync(planId, ct);
        if (plan is null || plan.RegisterEntryId != registerEntryId)
        {
            return FailPlan("Withdrawal plan not found.", 404, ControlledCopyReasonCodes.PlanNotFound, correlationId);
        }

        var copies = await _copies.GetByRegisterEntryAsync(registerEntryId, ct);
        var required = copies.Where(c => c.WithdrawalRequired).ToList();
        var withdrawn = required.Count(c => c.CopyStatus is ControlledCopyStatus.Withdrawn or ControlledCopyStatus.Reconciled or ControlledCopyStatus.Destroyed);
        var missing = required.Count(c => c.CopyStatus == ControlledCopyStatus.Missing);
        var obsolete = required.Count(c => c.CopyStatus == ControlledCopyStatus.Obsolete);
        var stillPending = required.Count(c => c.CopyStatus is ControlledCopyStatus.PendingWithdrawal or ControlledCopyStatus.Active);

        if (stillPending > 0)
        {
            plan.RequiredCopyCount = required.Count;
            plan.WithdrawnCopyCount = withdrawn;
            plan.MissingCopyCount = missing;
            plan.ObsoleteCopyCount = obsolete;
            plan.PlanStatus = CopyWithdrawalPlanStatus.Blocked;
            Touch(plan);
            await _plans.UpdateAsync(plan, ct);
            return FailPlan($"{stillPending} required copy/copies are not yet withdrawn/reconciled.", 409, ControlledCopyReasonCodes.PlanIncomplete, correlationId);
        }

        // A documented missing copy requires a deviation reference to close the plan (SOP §16).
        if (missing > 0 && string.IsNullOrWhiteSpace(input.MissingDeviationReference))
        {
            return FailPlan("A deviation reference is required to complete a plan with missing copies.", 400, ControlledCopyReasonCodes.DeviationRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        plan.RequiredCopyCount = required.Count;
        plan.WithdrawnCopyCount = withdrawn;
        plan.MissingCopyCount = missing;
        plan.ObsoleteCopyCount = obsolete;
        plan.PlanStatus = CopyWithdrawalPlanStatus.Completed;
        plan.PlanEvidenceReference = Trim(input.PlanEvidenceReference);
        plan.CompletedAt = now;
        plan.CompletedBy = _currentUser.ActorName;
        Touch(plan);
        await _plans.UpdateAsync(plan, ct);
        return Response<WithdrawalPlanModel>.Success(ControlledCopyWire.ToPlan(plan), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<WithdrawalPlanModel>>> ListPlansAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<IReadOnlyList<WithdrawalPlanModel>>.Fail("Register entry not found.", 404, ControlledCopyReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var rows = await _plans.GetByRegisterEntryAsync(registerEntryId, ct);
        return Response<IReadOnlyList<WithdrawalPlanModel>>.Success(rows.Select(ControlledCopyWire.ToPlan).ToList(), correlationId: correlationId);
    }

    public async Task<Response<CopyWithdrawalReadinessModel>> GetReadinessAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<CopyWithdrawalReadinessModel>.Fail("Register entry not found.", 404, ControlledCopyReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var copies = await _copies.GetByRegisterEntryAsync(registerEntryId, ct);
        var open = await _plans.GetOpenAsync(registerEntryId, ct);
        var findings = await _findings.GetByRegisterEntryAsync(registerEntryId, ct);
        return Response<CopyWithdrawalReadinessModel>.Success(_readiness.Evaluate(registerEntryId, copies, open, findings), correlationId: correlationId);
    }

    // ── obsolete reconciliation ───────────────────────────────────────────────

    public async Task<Response<IReadOnlyList<ObsoleteCopyFindingModel>>> EvaluateReconciliationAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<IReadOnlyList<ObsoleteCopyFindingModel>>.Fail("Register entry not found.", 404, ControlledCopyReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var copies = await _copies.GetByRegisterEntryAsync(registerEntryId, ct);
        var inUse = copies.Where(c => c.CopyStatus is ControlledCopyStatus.Active or ControlledCopyStatus.PendingWithdrawal).ToList();

        // A copy still in use for a document that is no longer in force is an obsolete-copy finding (SOP §6.2, §16).
        if (entry.LifecycleStatus is ControlledDocumentLifecycleStatus.Suspended or ControlledDocumentLifecycleStatus.Retired or ControlledDocumentLifecycleStatus.Superseded)
        {
            foreach (var copy in inUse)
            {
                var (type, severity, description) = entry.LifecycleStatus switch
                {
                    ControlledDocumentLifecycleStatus.Suspended => (ObsoleteCopyFindingType.SuspendedDocumentInUse, ObsoleteCopyFindingSeverity.Critical, "A suspended document is still in use at a point of use."),
                    ControlledDocumentLifecycleStatus.Retired => (ObsoleteCopyFindingType.RetiredCopyAvailable, ObsoleteCopyFindingSeverity.Critical, "A retired document copy is still available at a point of use."),
                    _ => (ObsoleteCopyFindingType.SupersededCopyAtPointOfUse, ObsoleteCopyFindingSeverity.Major, "A superseded document copy is still at a point of use.")
                };
                await RaiseFindingAsync(registerEntryId, copy.Id, type, severity, description, copy.LocationDescription, correlationId, ct);
            }
        }

        var all = await _findings.GetByRegisterEntryAsync(registerEntryId, ct);
        return Response<IReadOnlyList<ObsoleteCopyFindingModel>>.Success(all.Select(ControlledCopyWire.ToFinding).ToList(), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<ObsoleteCopyFindingModel>>> ListFindingsAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<IReadOnlyList<ObsoleteCopyFindingModel>>.Fail("Register entry not found.", 404, ControlledCopyReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var rows = await _findings.GetByRegisterEntryAsync(registerEntryId, ct);
        return Response<IReadOnlyList<ObsoleteCopyFindingModel>>.Success(rows.Select(ControlledCopyWire.ToFinding).ToList(), correlationId: correlationId);
    }

    public async Task<Response<ObsoleteCopyFindingModel>> ResolveFindingAsync(Guid registerEntryId, Guid findingId, ResolveObsoleteFindingInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<ObsoleteCopyFindingModel>.Fail("Register entry not found.", 404, ControlledCopyReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var finding = await _findings.GetByIdAsync(findingId, ct);
        if (finding is null || finding.RegisterEntryId != registerEntryId)
        {
            return Response<ObsoleteCopyFindingModel>.Fail("Finding not found.", 404, ControlledCopyReasonCodes.FindingNotFound, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.ResolutionEvidenceReference))
        {
            return Response<ObsoleteCopyFindingModel>.Fail("Resolution evidence is required.", 400, ControlledCopyReasonCodes.EvidenceRequired, correlationId);
        }

        finding.Status = ObsoleteCopyFindingStatus.Resolved;
        finding.ResolutionEvidenceReference = input.ResolutionEvidenceReference.Trim();
        finding.DeviationReference = Trim(input.DeviationReference);
        finding.QualityEventReference = Trim(input.QualityEventReference);
        finding.ResolvedAt = DateTimeOffset.UtcNow;
        finding.ResolvedBy = _currentUser.ActorName;
        Touch(finding);
        await _findings.UpdateAsync(finding, ct);
        return Response<ObsoleteCopyFindingModel>.Success(ControlledCopyWire.ToFinding(finding), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task RaiseFindingAsync(Guid registerEntryId, Guid? copyId, ObsoleteCopyFindingType type, ObsoleteCopyFindingSeverity severity,
        string description, string? location, string correlationId, CancellationToken ct)
    {
        var key = $"{type}:{copyId?.ToString() ?? "none"}";
        var existing = await _findings.GetByRegisterEntryAsync(registerEntryId, ct);
        if (existing.Any(x => x.FindingKey == key && x.Status is ObsoleteCopyFindingStatus.Open or ObsoleteCopyFindingStatus.Acknowledged))
        {
            return;
        }

        await _findings.CreateAsync(new DocumentObsoleteCopyFinding
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = registerEntryId,
            ControlledCopyId = copyId,
            FindingKey = key,
            FindingType = type,
            Severity = severity,
            Status = ObsoleteCopyFindingStatus.Open,
            DetectedAt = DateTimeOffset.UtcNow,
            DetectedBy = _currentUser.ActorName,
            LocationDescription = location,
            Description = description,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        }, ct);
    }

    private async Task ResolveCopyFindingsAsync(Guid copyId, CancellationToken ct)
    {
        // When a copy is withdrawn/reconciled, its open findings are resolved (evidence lives on the copy).
        var copy = await _copies.GetByIdAsync(copyId, ct);
        if (copy is null)
        {
            return;
        }

        var findings = await _findings.GetByRegisterEntryAsync(copy.RegisterEntryId, ct);
        foreach (var f in findings.Where(x => x.ControlledCopyId == copyId && x.Status == ObsoleteCopyFindingStatus.Open))
        {
            f.Status = ObsoleteCopyFindingStatus.Resolved;
            f.ResolvedAt = DateTimeOffset.UtcNow;
            f.ResolvedBy = _currentUser.ActorName;
            f.ResolutionEvidenceReference = copy.WithdrawalEvidenceReference ?? copy.ReconciliationEvidenceReference;
            Touch(f);
            await _findings.UpdateAsync(f, ct);
        }
    }

    private async Task<(Response<ControlledCopyModel>? Fail, DocumentControlledCopy? Copy)> LoadCopyAsync(Guid registerEntryId, Guid copyId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return (FailCopy("Register entry not found.", 404, ControlledCopyReasonCodes.NotFoundNonLeakage, correlationId), null);
        }

        var copy = await _copies.GetByIdAsync(copyId, ct);
        if (copy is null || copy.RegisterEntryId != registerEntryId)
        {
            return (FailCopy("Controlled copy not found.", 404, ControlledCopyReasonCodes.CopyNotFound, correlationId), null);
        }

        return (null, copy);
    }

    private void Touch(BaseEntity e)
    {
        e.UpdatedAt = DateTimeOffset.UtcNow;
        e.UpdatedBy = _currentUser.ActorName;
    }

    private static Response<ControlledCopyModel> FailCopy(string error, int status, string reason, string correlationId) =>
        Response<ControlledCopyModel>.Fail(error, status, reason, correlationId);

    private static Response<WithdrawalPlanModel> FailPlan(string error, int status, string reason, string correlationId) =>
        Response<WithdrawalPlanModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
