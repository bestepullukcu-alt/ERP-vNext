using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Services;

/// <summary>
/// MOD-0029-FU16 — repository / DMS assessment orchestration (GMG-QMS-SOP-0001 §11.1). Records an assessment, evaluates
/// it into findings, approves it against the SOP minimum content, and links it to a Document Master Register entry so
/// FU10 Gate 2 can consume it. It produces NO validation claim, implements NO e-signature, and runs NO backup — it only
/// records governance. No hard delete; an approved assessment can be superseded but never destroyed.
/// </summary>
public sealed class DocumentRepositoryAssessmentService
{
    private readonly IDocumentRepositoryAssessmentRepository _assessments;
    private readonly IDocumentRepositoryAssessmentFindingRepository _findings;
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly DocumentRepositoryAssessmentEvaluator _evaluator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentRepositoryAssessmentService(
        IDocumentRepositoryAssessmentRepository assessments,
        IDocumentRepositoryAssessmentFindingRepository findings,
        IDocumentMasterRegisterRepository register,
        DocumentRepositoryAssessmentEvaluator evaluator,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _assessments = assessments;
        _findings = findings;
        _register = register;
        _evaluator = evaluator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<RepositoryAssessmentModel>> CreateAsync(RepositoryAssessmentFieldsInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var type = RepositoryAssessmentWire.ParseType(input.RepositoryType);
        if (string.IsNullOrWhiteSpace(input.RepositoryName) || type is null)
        {
            return Fail("A repository name and a valid repository type are required.", 400, RepositoryAssessmentReasonCodes.NameAndTypeRequired, correlationId);
        }

        var assessment = new DocumentRepositoryAssessment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RepositoryKey = Normalize(input.RepositoryName),
            RepositoryName = input.RepositoryName.Trim(),
            RepositoryType = type.Value,
            AssessmentStatus = RepositoryAssessmentStatus.Draft,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        Apply(assessment, input);
        await _assessments.CreateAsync(assessment, ct);
        return Response<RepositoryAssessmentModel>.Success(RepositoryAssessmentWire.ToModel(assessment), 201, correlationId);
    }

    public async Task<Response<RepositoryAssessmentModel>> UpdateAsync(Guid id, RepositoryAssessmentFieldsInput input, string correlationId, CancellationToken ct)
    {
        var (fail, assessment) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (assessment!.AssessmentStatus is RepositoryAssessmentStatus.Approved or RepositoryAssessmentStatus.Superseded)
        {
            return Fail("An approved/superseded assessment cannot be edited; create a new assessment.", 409, RepositoryAssessmentReasonCodes.AlreadyDecided, correlationId);
        }

        var type = RepositoryAssessmentWire.ParseType(input.RepositoryType);
        if (string.IsNullOrWhiteSpace(input.RepositoryName) || type is null)
        {
            return Fail("A repository name and a valid repository type are required.", 400, RepositoryAssessmentReasonCodes.NameAndTypeRequired, correlationId);
        }

        assessment.RepositoryName = input.RepositoryName.Trim();
        assessment.RepositoryKey = Normalize(input.RepositoryName);
        assessment.RepositoryType = type.Value;
        Apply(assessment, input);
        assessment.UpdatedAt = DateTimeOffset.UtcNow;
        assessment.UpdatedBy = _currentUser.ActorName;
        await _assessments.UpdateAsync(assessment, ct);
        return Response<RepositoryAssessmentModel>.Success(RepositoryAssessmentWire.ToModel(assessment), correlationId: correlationId);
    }

    public async Task<Response<RepositoryAssessmentReadinessModel>> EvaluateAsync(Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var assessment = await _assessments.GetByIdAsync(id, ct);
        if (assessment is null)
        {
            return FailReadiness("Assessment not found.", 404, RepositoryAssessmentReasonCodes.AssessmentNotFound, correlationId);
        }

        var result = await ReconcileFindingsAsync(assessment, ct);

        // Move a Draft into UnderReview on first evaluation (never downgrade an Approved one here).
        if (assessment.AssessmentStatus == RepositoryAssessmentStatus.Draft)
        {
            assessment.AssessmentStatus = RepositoryAssessmentStatus.UnderReview;
            assessment.UpdatedAt = DateTimeOffset.UtcNow;
            assessment.UpdatedBy = _currentUser.ActorName;
            await _assessments.UpdateAsync(assessment, ct);
        }

        return Response<RepositoryAssessmentReadinessModel>.Success(BuildReadiness(assessment, result), correlationId: correlationId);
    }

    public async Task<Response<RepositoryAssessmentModel>> ApproveAsync(Guid id, ApproveRepositoryAssessmentInput input, string correlationId, CancellationToken ct)
    {
        var (fail, assessment) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (assessment!.AssessmentStatus is RepositoryAssessmentStatus.Approved or RepositoryAssessmentStatus.Rejected or RepositoryAssessmentStatus.Superseded)
        {
            return Fail($"The assessment is already {assessment.AssessmentStatus}.", 409, RepositoryAssessmentReasonCodes.AlreadyDecided, correlationId);
        }

        var role = RepositoryAssessmentWire.ParseRole(input.ApprovedByRole);
        if (role is null || !RepositoryAssessmentApprovers.IsPermitted(role.Value))
        {
            return Fail("A repository assessment must be approved by the GQD (with IT/CSV technical approval), SOP §11.2.", 409, RepositoryAssessmentReasonCodes.ApproverRoleInvalid, correlationId);
        }

        // The SOP minimum content must be present — a Critical finding blocks approval.
        var eval = _evaluator.Evaluate(assessment, DateTimeOffset.UtcNow);
        if (eval.Findings.Any(fnd => fnd.Severity == RepositoryFindingSeverity.Critical))
        {
            await ReconcileFindingsAsync(assessment, ct); // persist the findings so the caller can see them
            return Fail("Mandatory assessment content is missing; resolve the critical findings before approval.", 409, RepositoryAssessmentReasonCodes.RequiredFieldsMissing, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        assessment.AssessmentStatus = RepositoryAssessmentStatus.Approved;
        assessment.ApprovedByUserId = _currentUser.UserId;
        assessment.ApprovedByRole = role.Value.ToString();
        assessment.ApprovedAt = now;
        assessment.ValidFrom = now;
        assessment.ValidUntil = input.ValidUntil;
        assessment.UpdatedAt = now;
        assessment.UpdatedBy = _currentUser.ActorName;
        await _assessments.UpdateAsync(assessment, ct);
        return Response<RepositoryAssessmentModel>.Success(RepositoryAssessmentWire.ToModel(assessment), correlationId: correlationId);
    }

    public async Task<Response<RepositoryAssessmentModel>> RejectAsync(Guid id, RejectRepositoryAssessmentInput input, string correlationId, CancellationToken ct)
    {
        var (fail, assessment) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("A rejection reason is required.", 400, RepositoryAssessmentReasonCodes.ReasonRequired, correlationId);
        }

        assessment!.AssessmentStatus = RepositoryAssessmentStatus.Rejected;
        assessment.RejectionReason = input.Reason.Trim();
        assessment.UpdatedAt = DateTimeOffset.UtcNow;
        assessment.UpdatedBy = _currentUser.ActorName;
        await _assessments.UpdateAsync(assessment, ct);
        return Response<RepositoryAssessmentModel>.Success(RepositoryAssessmentWire.ToModel(assessment), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<RepositoryAssessmentModel>>> ListAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _assessments.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<RepositoryAssessmentModel>>.Success(rows.Select(RepositoryAssessmentWire.ToModel).ToList(), correlationId: correlationId);
    }

    public async Task<Response<RepositoryAssessmentModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, assessment) = await LoadAsync(id, correlationId, ct);
        return fail ?? Response<RepositoryAssessmentModel>.Success(RepositoryAssessmentWire.ToModel(assessment!), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<RepositoryAssessmentFindingModel>>> GetFindingsAsync(Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var assessment = await _assessments.GetByIdAsync(id, ct);
        if (assessment is null)
        {
            return Response<IReadOnlyList<RepositoryAssessmentFindingModel>>.Fail("Assessment not found.", 404, RepositoryAssessmentReasonCodes.AssessmentNotFound, correlationId);
        }

        var rows = await _findings.GetByAssessmentAsync(id, ct);
        return Response<IReadOnlyList<RepositoryAssessmentFindingModel>>.Success(rows.Select(RepositoryAssessmentWire.ToFinding).ToList(), correlationId: correlationId);
    }

    public async Task<Response<RepositoryAssessmentModel>> LinkToRegisterAsync(Guid registerEntryId, Guid assessmentId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, RepositoryAssessmentReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var assessment = await _assessments.GetByIdAsync(assessmentId, ct);
        if (assessment is null)
        {
            return Fail("Assessment not found.", 404, RepositoryAssessmentReasonCodes.AssessmentNotFound, correlationId);
        }

        if (assessment.AssessmentStatus is RepositoryAssessmentStatus.Rejected or RepositoryAssessmentStatus.Superseded)
        {
            return Fail($"A {assessment.AssessmentStatus} assessment cannot be linked.", 409, RepositoryAssessmentReasonCodes.LinkStatusInvalid, correlationId);
        }

        entry.ApprovedRepositoryId = assessment.Id.ToString();
        entry.ApprovedRepositoryName = assessment.RepositoryName;
        entry.ApprovedRepositoryPath = assessment.ExactLocation;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedBy = _currentUser.ActorName;
        await _register.UpdateAsync(entry, ct);
        return Response<RepositoryAssessmentModel>.Success(RepositoryAssessmentWire.ToModel(assessment), correlationId: correlationId);
    }

    public async Task<Response<RepositoryAssessmentModel>> GetLinkedAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, RepositoryAssessmentReasonCodes.NotFoundNonLeakage, correlationId);
        }

        if (!Guid.TryParse(entry.ApprovedRepositoryId, out var assessmentId))
        {
            return Fail("No repository assessment is linked to this document.", 404, RepositoryAssessmentReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var assessment = await _assessments.GetByIdAsync(assessmentId, ct);
        return assessment is null
            ? Fail("The linked repository assessment was not found.", 404, RepositoryAssessmentReasonCodes.AssessmentNotFound, correlationId)
            : Response<RepositoryAssessmentModel>.Success(RepositoryAssessmentWire.ToModel(assessment), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<DocumentRepositoryAssessmentEvaluator.Result> ReconcileFindingsAsync(DocumentRepositoryAssessment assessment, CancellationToken ct)
    {
        var result = _evaluator.Evaluate(assessment, DateTimeOffset.UtcNow);
        var existing = await _findings.GetByAssessmentAsync(assessment.Id, ct);
        var currentKeys = result.Findings.Select(x => x.Key).ToHashSet();
        var existingByKey = existing.ToDictionary(x => x.FindingKey);

        // Add newly-computed findings.
        foreach (var spec in result.Findings.Where(s => !existingByKey.ContainsKey(s.Key)))
        {
            await _findings.CreateAsync(new DocumentRepositoryAssessmentFinding
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantContext.TenantId,
                RepositoryAssessmentId = assessment.Id,
                FindingKey = spec.Key,
                FindingType = spec.Type,
                Severity = spec.Severity,
                Status = RepositoryFindingStatus.Open,
                Description = spec.Description,
                CreatedBy = _currentUser.ActorName
            }, ct);
        }

        // Auto-resolve OPEN findings that no longer apply (never hard-deleted).
        foreach (var stale in existing.Where(f => f.Status == RepositoryFindingStatus.Open && !currentKeys.Contains(f.FindingKey)))
        {
            stale.Status = RepositoryFindingStatus.Resolved;
            stale.UpdatedAt = DateTimeOffset.UtcNow;
            stale.UpdatedBy = _currentUser.ActorName;
            await _findings.UpdateAsync(stale, ct);
        }

        return result;
    }

    private static RepositoryAssessmentReadinessModel BuildReadiness(DocumentRepositoryAssessment a, DocumentRepositoryAssessmentEvaluator.Result r)
    {
        var blocking = r.Findings.Where(f => f.Severity == RepositoryFindingSeverity.Critical).Select(ToFindingModel).ToList();
        var warning = r.Findings.Where(f => f.Severity != RepositoryFindingSeverity.Critical).Select(ToFindingModel).ToList();
        var ready = r.CanSupportReleaseGate && blocking.Count == 0;
        return new RepositoryAssessmentReadinessModel(
            a.Id, a.RepositoryType.ToString(), a.AssessmentStatus.ToString(), ready,
            r.CanSupportReleaseGate, r.CanSupportRegulatedESignature, r.BoundaryStatement, blocking, warning);

        RepositoryAssessmentFindingModel ToFindingModel(DocumentRepositoryAssessmentEvaluator.FindingSpec s) =>
            new(Guid.Empty, a.Id, s.Key, s.Type.ToString(), s.Severity.ToString(), nameof(RepositoryFindingStatus.Open), s.Description, null);
    }

    private static void Apply(DocumentRepositoryAssessment a, RepositoryAssessmentFieldsInput i)
    {
        a.LocationType = RepositoryAssessmentWire.ParseLocation(i.LocationType);
        a.RepositoryOwnerUserId = i.RepositoryOwnerUserId == Guid.Empty ? null : i.RepositoryOwnerUserId;
        a.RepositoryOwnerRole = Trim(i.RepositoryOwnerRole);
        a.ExactLocation = Trim(i.ExactLocation);
        a.AccessModelDescription = Trim(i.AccessModelDescription);
        a.AccessReviewFrequency = Trim(i.AccessReviewFrequency);
        a.BackupMethodDescription = Trim(i.BackupMethodDescription);
        a.RestoreTestFrequency = Trim(i.RestoreTestFrequency);
        a.ApprovalMechanismDescription = Trim(i.ApprovalMechanismDescription);
        a.EffectiveCopyControlDescription = Trim(i.EffectiveCopyControlDescription);
        a.AuditTrailDescription = Trim(i.AuditTrailDescription);
        a.ChangeControlDescription = Trim(i.ChangeControlDescription);
        a.ValidationEvidenceReference = Trim(i.ValidationEvidenceReference);
        a.MaxInterimPeriodDays = i.MaxInterimPeriodDays;
        a.InterimCheckpointDueDate = i.InterimCheckpointDueDate;
        a.MigrationReconciliationRequired = i.MigrationReconciliationRequired;
        a.MigrationReconciliationReference = Trim(i.MigrationReconciliationReference);
        a.AssessmentEvidenceReference = Trim(i.AssessmentEvidenceReference);
    }

    private async Task<(Response<RepositoryAssessmentModel>? Fail, DocumentRepositoryAssessment? Assessment)> LoadAsync(Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var assessment = await _assessments.GetByIdAsync(id, ct);
        return assessment is null
            ? (Fail("Assessment not found.", 404, RepositoryAssessmentReasonCodes.AssessmentNotFound, correlationId), null)
            : (null, assessment);
    }

    private static Response<RepositoryAssessmentModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<RepositoryAssessmentModel>.Fail(error, status, reason, correlationId);

    private static Response<RepositoryAssessmentReadinessModel> FailReadiness(string error, int status, string reason, string correlationId) =>
        Response<RepositoryAssessmentReadinessModel>.Fail(error, status, reason, correlationId);

    private static string Normalize(string name) => name.Trim().ToUpperInvariant().Replace(' ', '-');
    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
