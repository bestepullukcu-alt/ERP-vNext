using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementApproval.Services;

/// <summary>
/// MOD-0029-FU09 — approval route requirement + evidence + segregation orchestration (GMG-QMS-SOP-0001 §5, §5.1, §7.2).
/// This is NOT an approval WORKFLOW engine (no task assignment / notification — that is a later MOD-0023 integration).
/// It resolves WHO must approve, records immutable evidence, evaluates segregation and computes readiness, writing the
/// aggregate result back to <c>DocumentMasterRegisterEntry.ApprovalEvidenceStatus</c> for FU10 to consume. No hard delete.
/// </summary>
public sealed class DocumentApprovalService
{
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IDocumentApprovalRequirementRepository _requirements;
    private readonly IDocumentApprovalEvidenceRepository _evidence;
    private readonly DocumentApprovalRouteResolver _resolver;
    private readonly DocumentSegregationRuleEvaluator _segregation;
    private readonly IApprovalRoleDirectory _roleDirectory;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentApprovalService(
        IDocumentMasterRegisterRepository register,
        IDocumentApprovalRequirementRepository requirements,
        IDocumentApprovalEvidenceRepository evidence,
        DocumentApprovalRouteResolver resolver,
        DocumentSegregationRuleEvaluator segregation,
        IApprovalRoleDirectory roleDirectory,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _register = register;
        _requirements = requirements;
        _evidence = evidence;
        _resolver = resolver;
        _segregation = segregation;
        _roleDirectory = roleDirectory;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<IReadOnlyList<ApprovalRequirementModel>>> ResolveRouteAsync(Guid registerEntryId, ResolveApprovalRouteInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return FailList("Register entry not found.", 404, ApprovalReasonCodes.NotFoundNonLeakage, correlationId);
        }

        ApplyOverrides(entry, input);
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedBy = _currentUser.ActorName;

        var specs = _resolver.Resolve(entry);
        var directoryRoleNames = specs
            .Where(spec => spec.Role != ApprovalRequiredRole.DocumentOwner)
            .Select(spec => spec.Role.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var directoryRoles = await _roleDirectory.ResolveAsync(directoryRoleNames, ct);
        var missingRoles = directoryRoleNames
            .Where(roleName => !directoryRoles.ContainsKey(roleName))
            .ToArray();
        if (missingRoles.Length > 0)
        {
            return FailList(
                $"Approval role configuration is missing in AuthService: {string.Join(", ", missingRoles)}.",
                409,
                ApprovalReasonCodes.RoleConfigurationMissing,
                correlationId);
        }

        var existing = await _requirements.GetByRegisterEntryAsync(registerEntryId, ct);
        var existingByKey = existing.ToDictionary(x => x.RequirementKey);
        var specKeys = specs.Select(s => s.Key).ToHashSet();

        // Idempotent reconcile: add missing requirements; keep existing (and their completed status) untouched.
        foreach (var spec in specs)
        {
            var directoryRole = spec.Role == ApprovalRequiredRole.DocumentOwner
                ? null
                : directoryRoles[spec.Role.ToString()];
            var requiredUserId = spec.Role == ApprovalRequiredRole.DocumentOwner
                ? entry.ProcessOwnerUserId
                : null;

            if (existingByKey.TryGetValue(spec.Key, out var current))
            {
                current.RequiredRoleId = directoryRole?.Id;
                current.RequiredRoleName = directoryRole?.Name;
                current.RequiredRoleDisplayName = directoryRole?.DisplayName;
                current.RequiredUserId = requiredUserId;
                current.UpdatedAt = DateTimeOffset.UtcNow;
                current.UpdatedBy = _currentUser.ActorName;
                await _requirements.UpdateAsync(current, ct);
                continue;
            }

            await _requirements.CreateAsync(new DocumentApprovalRequirement
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantContext.TenantId,
                RegisterEntryId = registerEntryId,
                RequirementKey = spec.Key,
                RequirementType = spec.Type,
                RequiredRole = spec.Role,
                RequiredRoleId = directoryRole?.Id,
                RequiredRoleName = directoryRole?.Name,
                RequiredRoleDisplayName = directoryRole?.DisplayName,
                RequiredUserId = requiredUserId,
                IsMandatory = spec.Mandatory,
                IsNonDelegable = spec.NonDelegable,
                SourceRule = spec.Source,
                Status = ApprovalRequirementStatus.Pending,
                CreatedBy = _currentUser.ActorName
            }, ct);
        }

        // Retire requirements the current route no longer needs (e.g. criticality/class was lowered). Only PENDING
        // requirements are retired — any requirement that already carries a decision (Completed / Rejected / Waived /
        // Blocked) is immutable evidence and is preserved. Soft-delete via IsDeleted keeps them out of every read while
        // remaining fully recoverable for audit (SOP §5: no hard delete).
        foreach (var stale in existing.Where(x =>
                     x.Status == ApprovalRequirementStatus.Pending && !specKeys.Contains(x.RequirementKey)))
        {
            stale.IsDeleted = true;
            stale.DeletedAt = DateTimeOffset.UtcNow;
            stale.UpdatedAt = DateTimeOffset.UtcNow;
            stale.UpdatedBy = _currentUser.ActorName;
            await _requirements.UpdateAsync(stale, ct);
        }

        var all = await _requirements.GetByRegisterEntryAsync(registerEntryId, ct);
        await ApplyReadinessToEntryAsync(entry, all, ct);

        return Response<IReadOnlyList<ApprovalRequirementModel>>.Success(all.Select(ApprovalWire.ToRequirement).ToList(), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<ApprovalRequirementModel>>> GetRequirementsAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return FailList("Register entry not found.", 404, ApprovalReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var all = await _requirements.GetByRegisterEntryAsync(registerEntryId, ct);
        return Response<IReadOnlyList<ApprovalRequirementModel>>.Success(all.Select(ApprovalWire.ToRequirement).ToList(), correlationId: correlationId);
    }

    public Task<Response<ApprovalReadinessModel>> RecordEvidenceAsync(Guid registerEntryId, RecordApprovalEvidenceInput input, string correlationId, CancellationToken ct) =>
        WriteEvidenceAsync(registerEntryId, input.RequirementId, input.Action, input.PerformedByUserId, input.PerformedByRole,
            input.EvidenceReference, input.Comment, correlationId, ct);

    public Task<Response<ApprovalReadinessModel>> RejectAsync(Guid registerEntryId, RejectApprovalInput input, string correlationId, CancellationToken ct) =>
        WriteEvidenceAsync(registerEntryId, input.RequirementId, nameof(ApprovalEvidenceAction.Rejected), input.PerformedByUserId, input.PerformedByRole,
            evidenceReference: null, comment: string.IsNullOrWhiteSpace(input.Comment) ? input.Reason : $"{input.Reason} — {input.Comment}", correlationId, ct);

    public async Task<Response<ApprovalReadinessModel>> GetReadinessAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<ApprovalReadinessModel>.Fail("Register entry not found.", 404, ApprovalReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var all = await _requirements.GetByRegisterEntryAsync(registerEntryId, ct);
        var (model, _) = ComputeReadiness(entry, all);
        return Response<ApprovalReadinessModel>.Success(model, correlationId: correlationId);
    }

    // ── core evidence write ──────────────────────────────────────────────────────

    private async Task<Response<ApprovalReadinessModel>> WriteEvidenceAsync(
        Guid registerEntryId, Guid requirementId, string action, Guid performedByUserId, string performedByRole,
        string? evidenceReference, string? comment, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        var parsedAction = ApprovalWire.ParseAction(action);
        var parsedRole = ApprovalWire.ParseRole(performedByRole);
        if (parsedAction is null || parsedRole is null)
        {
            return Response<ApprovalReadinessModel>.Fail("A valid action and performer role are required.", 400, ApprovalReasonCodes.ValidationFailed, correlationId);
        }

        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<ApprovalReadinessModel>.Fail("Register entry not found.", 404, ApprovalReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var requirement = await _requirements.GetByIdAsync(requirementId, ct);
        if (requirement is null || requirement.RegisterEntryId != registerEntryId)
        {
            return Response<ApprovalReadinessModel>.Fail("Approval requirement not found.", 404, ApprovalReasonCodes.RequirementNotFound, correlationId);
        }

        var authenticatedUserId = _currentUser.UserId;
        if (authenticatedUserId == Guid.Empty || performedByUserId != authenticatedUserId)
        {
            return Response<ApprovalReadinessModel>.Fail(
                "Approval evidence can only be recorded for the authenticated user.",
                403,
                ApprovalReasonCodes.ApproverIdentityMismatch,
                correlationId);
        }

        // The client-provided role is only a consistency check. Authorization is derived from the persisted
        // requirement and AuthService assignment, never from this string.
        if (parsedRole.Value != requirement.RequiredRole)
        {
            return Response<ApprovalReadinessModel>.Fail(
                $"This requirement must be actioned by {requirement.RequiredRole}, not {parsedRole}.", 409, ApprovalReasonCodes.WrongRole, correlationId);
        }

        if (requirement.RequiredRole == ApprovalRequiredRole.DocumentOwner)
        {
            if (requirement.RequiredUserId is null || requirement.RequiredUserId != authenticatedUserId)
            {
                return Response<ApprovalReadinessModel>.Fail(
                    "Only the document's assigned owner may action this requirement.",
                    403,
                    ApprovalReasonCodes.ApproverNotAssigned,
                    correlationId);
            }
        }
        else
        {
            if (requirement.RequiredRoleId is null
                || !await _roleDirectory.UserHasRoleAsync(authenticatedUserId, requirement.RequiredRoleId.Value, ct))
            {
                return Response<ApprovalReadinessModel>.Fail(
                    $"The authenticated user is not assigned to the required {requirement.RequiredRoleDisplayName ?? requirement.RequiredRoleName ?? requirement.RequiredRole.ToString()} role.",
                    403,
                    ApprovalReasonCodes.ApproverNotAssigned,
                    correlationId);
            }
        }

        var isRejection = parsedAction is ApprovalEvidenceAction.Rejected or ApprovalEvidenceAction.Returned;
        var previousStatus = requirement.Status;
        var previousCompletedByUserId = requirement.CompletedByUserId;
        var previousCompletedByRole = requirement.CompletedByRole;
        var previousCompletedAt = requirement.CompletedAt;
        var previousEvidenceReference = requirement.EvidenceReference;
        var previousComment = requirement.Comment;

        requirement.Status = isRejection ? ApprovalRequirementStatus.Rejected : ApprovalRequirementStatus.Completed;
        requirement.CompletedByUserId = authenticatedUserId;
        requirement.CompletedByRole = requirement.RequiredRole;
        requirement.CompletedAt = DateTimeOffset.UtcNow;
        requirement.EvidenceReference = TrimOrNull(evidenceReference);
        requirement.Comment = TrimOrNull(comment);

        var all = await _requirements.GetByRegisterEntryAsync(registerEntryId, ct);
        // MongoDB returns detached instances. The requirement loaded above is therefore not guaranteed to be the
        // same object as the matching row in this list. Evaluate the prospective route with the newly mutated
        // requirement explicitly substituted, otherwise the final approval is still seen as Pending and both
        // segregation and the persisted aggregate status are computed from stale data.
        var candidateRequirements = all
            .Select(r => r.Id == requirement.Id ? requirement : r)
            .ToList();
        var allMandatoryComplete = candidateRequirements.Where(r => r.IsMandatory)
            .All(r => r.Status == ApprovalRequirementStatus.Completed);
        var failures = allMandatoryComplete ? _segregation.Evaluate(entry, candidateRequirements) : [];
        if (!isRejection && failures.Count > 0)
        {
            requirement.Status = previousStatus;
            requirement.CompletedByUserId = previousCompletedByUserId;
            requirement.CompletedByRole = previousCompletedByRole;
            requirement.CompletedAt = previousCompletedAt;
            requirement.EvidenceReference = previousEvidenceReference;
            requirement.Comment = previousComment;
            return Response<ApprovalReadinessModel>.Fail(
                failures[0],
                409,
                ApprovalReasonCodes.SegregationFailed,
                correlationId);
        }

        requirement.UpdatedAt = DateTimeOffset.UtcNow;
        requirement.UpdatedBy = _currentUser.ActorName;
        await _requirements.UpdateAsync(requirement, ct);

        await _evidence.CreateAsync(new DocumentApprovalEvidence
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = registerEntryId,
            RequirementId = requirementId,
            Action = parsedAction.Value,
            PerformedByUserId = authenticatedUserId,
            PerformedByRole = requirement.RequiredRole,
            PerformedAt = DateTimeOffset.UtcNow,
            EvidenceReference = TrimOrNull(evidenceReference),
            Comment = TrimOrNull(comment),
            IsSegregationChecked = true,
            SegregationResult = failures.Count == 0 ? SegregationResult.Passed : SegregationResult.Failed,
            FailureReason = failures.FirstOrDefault(),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        }, ct);

        // Re-read after the requirement write so the aggregate stored on the register entry is derived from the
        // persisted route, not from the pre-update snapshot used for validation above.
        var persistedRequirements = await _requirements.GetByRegisterEntryAsync(registerEntryId, ct);
        var (model, _) = await ApplyReadinessToEntryAsync(entry, persistedRequirements, ct);
        return Response<ApprovalReadinessModel>.Success(model, correlationId: correlationId);
    }

    // ── readiness computation ────────────────────────────────────────────────────

    private async Task<(ApprovalReadinessModel Model, ApprovalEvidenceState State)> ApplyReadinessToEntryAsync(
        DocumentMasterRegisterEntry entry, IReadOnlyList<DocumentApprovalRequirement> requirements, CancellationToken ct)
    {
        var (model, state) = ComputeReadiness(entry, requirements);
        entry.ApprovalEvidenceStatus = state.ToString();
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedBy = _currentUser.ActorName;
        await _register.UpdateAsync(entry, ct);
        return (model, state);
    }

    private (ApprovalReadinessModel Model, ApprovalEvidenceState State) ComputeReadiness(
        DocumentMasterRegisterEntry entry, IReadOnlyList<DocumentApprovalRequirement> requirements)
    {
        var required = requirements.Count;
        var completed = requirements.Count(r => r.Status == ApprovalRequirementStatus.Completed);
        var pending = requirements.Count(r => r.Status == ApprovalRequirementStatus.Pending);
        var rejected = requirements.Count(r => r.Status == ApprovalRequirementStatus.Rejected);
        var blocked = requirements.Count(r => r.Status == ApprovalRequirementStatus.Blocked);

        var missingMandatory = requirements
            .Where(r => r.IsMandatory && r.Status != ApprovalRequirementStatus.Completed)
            .Select(r => r.RequiredRole.ToString())
            .Distinct()
            .ToList();

        var allMandatoryComplete = requirements
            .Where(r => r.IsMandatory)
            .All(r => r.Status == ApprovalRequirementStatus.Completed);

        // "Author is the sole approver" is a final-route assertion, not an intermediate-state assertion. While
        // mandatory approvals are still pending, the author may legitimately be the first approver and must not be
        // reported as the sole approver yet. RecordEvidenceAsync and the lifecycle gate already evaluate segregation
        // only after all mandatory requirements complete; readiness must follow the same rule.
        var failures = allMandatoryComplete
            ? _segregation.Evaluate(entry, requirements).ToList()
            : [];
        if (!DocumentLinkGovernanceGuard.IsGovernedRelationCompatible(entry))
        {
            failures.Add(DocumentLinkGovernanceGuard.BlockingReason);
        }

        ApprovalEvidenceState state;
        if (required == 0)
        {
            state = ApprovalEvidenceState.NotRequired;
        }
        else if (rejected > 0)
        {
            state = ApprovalEvidenceState.Rejected;
        }
        else if (blocked > 0)
        {
            state = ApprovalEvidenceState.Blocked;
        }
        else if (!allMandatoryComplete)
        {
            state = ApprovalEvidenceState.Pending;
        }
        else if (failures.Count > 0)
        {
            state = ApprovalEvidenceState.SegregationFailed;
        }
        else
        {
            state = ApprovalEvidenceState.Complete;
        }

        var ready = state == ApprovalEvidenceState.Complete;

        var model = new ApprovalReadinessModel(
            entry.Id, required, completed, pending, rejected, blocked,
            failures, missingMandatory, ready, state.ToString());
        return (model, state);
    }

    private static void ApplyOverrides(DocumentMasterRegisterEntry e, ResolveApprovalRouteInput i)
    {
        if (i.HasRaImpact is { } ra) e.HasRaImpact = ra;
        if (i.HasPvImpact is { } pv) e.HasPvImpact = pv;
        if (i.HasBatchReleaseImpact is { } br) e.HasBatchReleaseImpact = br;
        if (i.HasDmsCsvImpact is { } dms) e.HasDmsCsvImpact = dms;
        if (i.HasQualityAgreementImpact is { } qa) e.HasQualityAgreementImpact = qa;
        if (i.IsGroupGovernance is { } gg) e.IsGroupGovernance = gg;
        if (i.RequiresLegalReview is { } lg) e.RequiresLegalReview = lg;
        if (i.RequiresCeoEndorsement is { } ceo) e.RequiresCeoEndorsement = ceo;
        if (i.RequiresIndependentTechnicalReview is { } itr) e.RequiresIndependentTechnicalReview = itr;
        if (i.AuthorUserId is { } author) e.AuthorUserId = author == Guid.Empty ? null : author;
        if (i.RequestedByUserId is { } req) e.RequestedByUserId = req == Guid.Empty ? null : req;
    }

    private static Response<IReadOnlyList<ApprovalRequirementModel>> FailList(string error, int status, string reason, string correlationId) =>
        Response<IReadOnlyList<ApprovalRequirementModel>>.Fail(error, status, reason, correlationId);

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
