using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementTraining.Services;

/// <summary>
/// MOD-0029-FU11 — document training matrix orchestration (GMG-QMS-SOP-0001 §7.3, §9.11, §17). Resolves role-to-
/// document training requirements, records assignments / completions / effectiveness checks / formal restrictions, and
/// computes readiness that FU10 Gate 5 consumes. NOT an LMS: user-level rostering and HCM/LMS wiring are extension
/// points (completion evidence may reference an external LMS record). No hard delete; no waiver.
/// </summary>
public sealed class DocumentTrainingService
{
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IDocumentTrainingMatrixRequirementRepository _requirements;
    private readonly IDocumentTrainingAssignmentRepository _assignments;
    private readonly DocumentTrainingMatrixResolver _resolver;
    private readonly DocumentTrainingReadinessEvaluator _readiness;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentTrainingService(
        IDocumentMasterRegisterRepository register,
        IDocumentTrainingMatrixRequirementRepository requirements,
        IDocumentTrainingAssignmentRepository assignments,
        DocumentTrainingMatrixResolver resolver,
        DocumentTrainingReadinessEvaluator readiness,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _register = register;
        _requirements = requirements;
        _assignments = assignments;
        _resolver = resolver;
        _readiness = readiness;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<IReadOnlyList<TrainingRequirementModel>>> ResolveMatrixAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return FailReqs("Register entry not found.", 404, TrainingReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var specs = _resolver.Resolve(entry);
        var existing = await _requirements.GetByRegisterEntryAsync(registerEntryId, ct);
        var existingKeys = existing.Select(x => x.RequirementKey).ToHashSet();

        foreach (var spec in specs.Where(s => !existingKeys.Contains(s.Key)))
        {
            await _requirements.CreateAsync(new DocumentTrainingMatrixRequirement
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantContext.TenantId,
                RegisterEntryId = registerEntryId,
                RequirementKey = spec.Key,
                AudienceType = spec.Audience,
                RequiredRole = spec.Role,
                TrainingType = spec.TrainingType,
                IsCriticalProcessUserRequirement = spec.CriticalProcessUser,
                EffectivenessCheckRequired = spec.EffectivenessCheck,
                AcknowledgementRequired = spec.Acknowledgement,
                MandatoryBeforeEffective = spec.MandatoryBeforeEffective,
                SourceRule = spec.Source,
                Status = TrainingRequirementStatus.Pending,
                CreatedBy = _currentUser.ActorName
            }, ct);
        }

        var all = await _requirements.GetByRegisterEntryAsync(registerEntryId, ct);
        return Response<IReadOnlyList<TrainingRequirementModel>>.Success(all.Select(TrainingWire.ToRequirement).ToList(), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<TrainingRequirementModel>>> GetRequirementsAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return FailReqs("Register entry not found.", 404, TrainingReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var all = await _requirements.GetByRegisterEntryAsync(registerEntryId, ct);
        return Response<IReadOnlyList<TrainingRequirementModel>>.Success(all.Select(TrainingWire.ToRequirement).ToList(), correlationId: correlationId);
    }

    public async Task<Response<TrainingRequirementModel>> AddManualRequirementAsync(Guid registerEntryId, AddManualTrainingRequirementInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<TrainingRequirementModel>.Fail("Register entry not found.", 404, TrainingReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var audience = TrainingWire.ParseAudience(input.AudienceType);
        var trainingType = TrainingWire.ParseTrainingType(input.TrainingType);
        if (audience is null || trainingType is null)
        {
            return Response<TrainingRequirementModel>.Fail("A valid audience type and training type are required.", 400, TrainingReasonCodes.ValidationFailed, correlationId);
        }

        var role = TrainingWire.ParseRole(input.RequiredRole);
        var key = $"{audience}:{role?.ToString() ?? "-"}:{trainingType}";
        var existing = await _requirements.GetByRegisterEntryAsync(registerEntryId, ct);
        var duplicate = existing.FirstOrDefault(x => x.RequirementKey == key);
        if (duplicate is not null)
        {
            return Response<TrainingRequirementModel>.Success(TrainingWire.ToRequirement(duplicate), correlationId: correlationId);
        }

        var requirement = new DocumentTrainingMatrixRequirement
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = registerEntryId,
            RequirementKey = key,
            AudienceType = audience.Value,
            RequiredRole = role,
            RequiredUserId = input.RequiredUserId,
            RequiredDepartment = TrimOrNull(input.RequiredDepartment),
            TrainingType = trainingType.Value,
            IsCriticalProcessUserRequirement = input.IsCriticalProcessUserRequirement,
            EffectivenessCheckRequired = input.EffectivenessCheckRequired,
            AcknowledgementRequired = input.AcknowledgementRequired,
            MandatoryBeforeEffective = input.MandatoryBeforeEffective,
            SourceRule = TrainingSourceRule.Manual,
            Status = TrainingRequirementStatus.Pending,
            CreatedBy = _currentUser.ActorName
        };
        await _requirements.CreateAsync(requirement, ct);
        return Response<TrainingRequirementModel>.Success(TrainingWire.ToRequirement(requirement), 201, correlationId);
    }

    public async Task<Response<TrainingAssignmentModel>> AssignAsync(Guid registerEntryId, AssignTrainingInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return FailAssign("Register entry not found.", 404, TrainingReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var requirement = await _requirements.GetByIdAsync(input.RequirementId, ct);
        if (requirement is null || requirement.RegisterEntryId != registerEntryId)
        {
            return FailAssign("Training requirement not found.", 404, TrainingReasonCodes.RequirementNotFound, correlationId);
        }

        var assignment = new DocumentTrainingAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = registerEntryId,
            RequirementId = requirement.Id,
            AssignedToUserId = input.AssignedToUserId,
            AssignedToRole = TrainingWire.ParseRole(input.AssignedToRole),
            AssignedToDepartment = TrimOrNull(input.AssignedToDepartment),
            TrainingType = requirement.TrainingType,
            AssignedAt = DateTimeOffset.UtcNow,
            AssignedBy = _currentUser.ActorName,
            DueDate = input.DueDate,
            Status = TrainingAssignmentStatus.Assigned,
            EffectivenessCheckStatus = requirement.EffectivenessCheckRequired ? TrainingEffectivenessCheckStatus.Pending : TrainingEffectivenessCheckStatus.NotRequired,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _assignments.CreateAsync(assignment, ct);

        requirement.Status = TrainingRequirementStatus.Assigned;
        requirement.UpdatedAt = DateTimeOffset.UtcNow;
        requirement.UpdatedBy = _currentUser.ActorName;
        await _requirements.UpdateAsync(requirement, ct);

        return Response<TrainingAssignmentModel>.Success(TrainingWire.ToAssignment(assignment), 201, correlationId);
    }

    public async Task<Response<TrainingAssignmentModel>> CompleteAsync(Guid registerEntryId, Guid assignmentId, CompleteTrainingInput input, string correlationId, CancellationToken ct)
    {
        var (fail, assignment) = await LoadAssignmentAsync(registerEntryId, assignmentId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.CompletionEvidenceReference))
        {
            return FailAssign("A completion evidence reference is required.", 400, TrainingReasonCodes.EvidenceRequired, correlationId);
        }

        assignment!.Status = TrainingAssignmentStatus.Completed;
        assignment.CompletionEvidenceReference = input.CompletionEvidenceReference.Trim();
        assignment.CompletedAt = DateTimeOffset.UtcNow;
        assignment.CompletedBy = _currentUser.ActorName;
        assignment.UpdatedAt = DateTimeOffset.UtcNow;
        assignment.UpdatedBy = _currentUser.ActorName;
        await _assignments.UpdateAsync(assignment, ct);

        await MarkRequirementCompletedIfSatisfiedAsync(assignment.RequirementId, ct);
        return Response<TrainingAssignmentModel>.Success(TrainingWire.ToAssignment(assignment), correlationId: correlationId);
    }

    public async Task<Response<TrainingAssignmentModel>> RecordEffectivenessAsync(Guid registerEntryId, Guid assignmentId, RecordEffectivenessInput input, string correlationId, CancellationToken ct)
    {
        var (fail, assignment) = await LoadAssignmentAsync(registerEntryId, assignmentId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (input.Passed && string.IsNullOrWhiteSpace(input.EvidenceReference))
        {
            return FailAssign("An evidence reference is required to pass the effectiveness check.", 400, TrainingReasonCodes.EvidenceRequired, correlationId);
        }

        assignment!.EffectivenessCheckStatus = input.Passed ? TrainingEffectivenessCheckStatus.Passed : TrainingEffectivenessCheckStatus.Failed;
        assignment.EffectivenessEvidenceReference = TrimOrNull(input.EvidenceReference);
        if (!input.Passed)
        {
            assignment.Status = TrainingAssignmentStatus.Failed;
        }
        assignment.UpdatedAt = DateTimeOffset.UtcNow;
        assignment.UpdatedBy = _currentUser.ActorName;
        await _assignments.UpdateAsync(assignment, ct);

        await MarkRequirementCompletedIfSatisfiedAsync(assignment.RequirementId, ct);
        return Response<TrainingAssignmentModel>.Success(TrainingWire.ToAssignment(assignment), correlationId: correlationId);
    }

    public async Task<Response<TrainingAssignmentModel>> RestrictAsync(Guid registerEntryId, Guid assignmentId, RestrictTrainingInput input, string correlationId, CancellationToken ct)
    {
        var (fail, assignment) = await LoadAssignmentAsync(registerEntryId, assignmentId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return FailAssign("A restriction reason is required (formal restriction from independent execution).", 400, TrainingReasonCodes.ReasonRequired, correlationId);
        }

        assignment!.Status = TrainingAssignmentStatus.Restricted;
        assignment.RestrictionReason = input.Reason.Trim();
        assignment.RestrictedAt = DateTimeOffset.UtcNow;
        assignment.RestrictedBy = _currentUser.ActorName;
        assignment.UpdatedAt = DateTimeOffset.UtcNow;
        assignment.UpdatedBy = _currentUser.ActorName;
        await _assignments.UpdateAsync(assignment, ct);

        await MarkRequirementCompletedIfSatisfiedAsync(assignment.RequirementId, ct);
        return Response<TrainingAssignmentModel>.Success(TrainingWire.ToAssignment(assignment), correlationId: correlationId);
    }

    public async Task<Response<TrainingReadinessModel>> GetReadinessAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<TrainingReadinessModel>.Fail("Register entry not found.", 404, TrainingReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var requirements = await _requirements.GetByRegisterEntryAsync(registerEntryId, ct);
        var assignments = await _assignments.GetByRegisterEntryAsync(registerEntryId, ct);
        return Response<TrainingReadinessModel>.Success(_readiness.Evaluate(entry, requirements, assignments), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<(Response<TrainingAssignmentModel>? Fail, DocumentTrainingAssignment? Assignment)> LoadAssignmentAsync(
        Guid registerEntryId, Guid assignmentId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return (FailAssign("Register entry not found.", 404, TrainingReasonCodes.NotFoundNonLeakage, correlationId), null);
        }

        var assignment = await _assignments.GetByIdAsync(assignmentId, ct);
        if (assignment is null || assignment.RegisterEntryId != registerEntryId)
        {
            return (FailAssign("Training assignment not found.", 404, TrainingReasonCodes.AssignmentNotFound, correlationId), null);
        }

        return (null, assignment);
    }

    private async Task MarkRequirementCompletedIfSatisfiedAsync(Guid requirementId, CancellationToken ct)
    {
        var requirement = await _requirements.GetByIdAsync(requirementId, ct);
        if (requirement is null)
        {
            return;
        }

        var reqAssignments = await _assignments.GetByRequirementAsync(requirementId, ct);
        var restricted = reqAssignments.Any(a => a.Status == TrainingAssignmentStatus.Restricted);
        var satisfied = restricted || (requirement.IsCriticalProcessUserRequirement
            ? reqAssignments.Any(a => a.Status == TrainingAssignmentStatus.Completed && a.EffectivenessCheckStatus == TrainingEffectivenessCheckStatus.Passed)
            : reqAssignments.Any(a => a.Status == TrainingAssignmentStatus.Completed
                && (!requirement.EffectivenessCheckRequired || a.EffectivenessCheckStatus == TrainingEffectivenessCheckStatus.Passed)));

        var newStatus = restricted ? TrainingRequirementStatus.Restricted
            : satisfied ? TrainingRequirementStatus.Completed
            : TrainingRequirementStatus.Assigned;

        if (requirement.Status != newStatus)
        {
            requirement.Status = newStatus;
            requirement.UpdatedAt = DateTimeOffset.UtcNow;
            requirement.UpdatedBy = _currentUser.ActorName;
            await _requirements.UpdateAsync(requirement, ct);
        }
    }

    private static Response<IReadOnlyList<TrainingRequirementModel>> FailReqs(string error, int status, string reason, string correlationId) =>
        Response<IReadOnlyList<TrainingRequirementModel>>.Fail(error, status, reason, correlationId);

    private static Response<TrainingAssignmentModel> FailAssign(string error, int status, string reason, string correlationId) =>
        Response<TrainingAssignmentModel>.Fail(error, status, reason, correlationId);

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
