using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementReleaseGates.Services;

/// <summary>
/// MOD-0029-FU10 — the non-waivable release gate engine (GMG-QMS-SOP-0001 §19, §21). It evaluates the six document
/// gates against the register entry, consuming FU07 (UID/code → gate 1), FU09 (ApprovalEvidenceStatus → gate 3) and
/// manual evidence (gates 2/4/5/6). Gate results are COMPUTED (never client-set): a request can only record evidence,
/// and a "Yes" always requires a reference, a verifier and a date. There is no exception/waiver — every gate is
/// non-waivable. Each evaluation is persisted immutably (history preserved); the entry's LastReleaseGate* extension
/// fields are updated. This is the DOCUMENT gate, not the MOD-0028 baseline qualification gate.
/// </summary>
public sealed class DocumentReleaseGateEvaluator
{
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IDocumentReleaseGateEvaluationRepository _evaluations;
    private readonly IDocumentReleaseGateResultRepository _results;
    private readonly IDocumentReleaseGateEvidenceRepository _evidence;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly DocumentReleaseGateOptions _options;
    private readonly ITrainingReadinessPort? _trainingPort;
    private readonly IRepositoryReadinessPort? _repositoryPort;
    private readonly ICopyReconciliationPort? _copyPort;

    public DocumentReleaseGateEvaluator(
        IDocumentMasterRegisterRepository register,
        IDocumentReleaseGateEvaluationRepository evaluations,
        IDocumentReleaseGateResultRepository results,
        IDocumentReleaseGateEvidenceRepository evidence,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        IOptions<DocumentReleaseGateOptions> options,
        // MOD-0029-FU11 training readiness port (optional). Null → existing manual/auto Gate 5 behaviour.
        ITrainingReadinessPort? trainingPort = null,
        // MOD-0029-FU16 repository readiness port (optional). Null → existing manual Gate 2 behaviour.
        IRepositoryReadinessPort? repositoryPort = null,
        // MOD-0029-FU17 copy reconciliation port (optional). Null → existing manual Gate 6 behaviour.
        ICopyReconciliationPort? copyPort = null)
    {
        _register = register;
        _evaluations = evaluations;
        _results = results;
        _evidence = evidence;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _options = options.Value;
        _trainingPort = trainingPort;
        _repositoryPort = repositoryPort;
        _copyPort = copyPort;
    }

    // ── public: evaluate + persist ──────────────────────────────────────────────

    public async Task<Response<ReleaseGateEvaluationModel>> EvaluateAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, ReleaseGateReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var (evaluation, results) = await EvaluateCoreAsync(entry, correlationId, ct);
        await _register.UpdateAsync(entry, ct);
        return Response<ReleaseGateEvaluationModel>.Success(ReleaseGateWire.ToEvaluation(evaluation, results), correlationId: correlationId);
    }

    public async Task<Response<ReleaseGateEvaluationModel>> RecordEvidenceAsync(Guid registerEntryId, RecordReleaseGateEvidenceInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        var gateKey = ReleaseGateCatalog.ParseKey(input.GateKey);
        if (gateKey is null)
        {
            return Fail("Unknown gate key.", 400, ReleaseGateReasonCodes.InvalidGateKey, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.EvidenceReference))
        {
            return Fail("An evidence reference is required; a gate marked met without evidence is not met (SOP §19.1).", 400, ReleaseGateReasonCodes.EvidenceIncomplete, correlationId);
        }

        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, ReleaseGateReasonCodes.NotFoundNonLeakage, correlationId);
        }

        await _evidence.CreateAsync(new DocumentReleaseGateEvidence
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = registerEntryId,
            GateKey = gateKey.Value,
            EvidenceReference = input.EvidenceReference.Trim(),
            // Verifier + date are mandatory; default to the current user / now when not supplied.
            VerifiedByUserId = input.VerifiedByUserId is { } v && v != Guid.Empty ? v : _currentUser.UserId,
            VerifiedByRole = TrimOrNull(input.VerifiedByRole),
            VerificationDate = input.VerificationDate ?? DateTimeOffset.UtcNow,
            Comment = TrimOrNull(input.Comment),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        }, ct);

        var (evaluation, results) = await EvaluateCoreAsync(entry, correlationId, ct);
        await _register.UpdateAsync(entry, ct);
        return Response<ReleaseGateEvaluationModel>.Success(ReleaseGateWire.ToEvaluation(evaluation, results), correlationId: correlationId);
    }

    public async Task<Response<ReleaseGateEvaluationModel>> GetLatestAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, ReleaseGateReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var latest = await _evaluations.GetLatestAsync(registerEntryId, ct);
        if (latest is null)
        {
            return Fail("No release-gate evaluation exists yet.", 404, ReleaseGateReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var results = await _results.GetByEvaluationAsync(latest.Id, ct);
        return Response<ReleaseGateEvaluationModel>.Success(ReleaseGateWire.ToEvaluation(latest, results), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<ReleaseGateEvaluationModel>>> GetHistoryAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<IReadOnlyList<ReleaseGateEvaluationModel>>.Fail("Register entry not found.", 404, ReleaseGateReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var evaluations = await _evaluations.GetHistoryAsync(registerEntryId, ct);
        var models = new List<ReleaseGateEvaluationModel>();
        foreach (var e in evaluations)
        {
            models.Add(ReleaseGateWire.ToEvaluation(e, await _results.GetByEvaluationAsync(e.Id, ct)));
        }

        return Response<IReadOnlyList<ReleaseGateEvaluationModel>>.Success(models, correlationId: correlationId);
    }

    /// <summary>Read-only readiness: computes the gates in memory WITHOUT persisting a new evaluation.</summary>
    public async Task<Response<ReleaseGateEvaluationModel>> GetReadinessAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, ReleaseGateReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var (status, computations) = await ComputeAsync(entry, ct);
        var model = new ReleaseGateEvaluationModel(
            Guid.Empty, entry.Id, status.ToString(), status == ReleaseGateEvaluationStatus.Complete,
            computations.Count, computations.Count(c => c.Result == ReleaseGateResultValue.Yes),
            computations.Count(c => c.Result == ReleaseGateResultValue.No),
            computations.Count(c => c.WarningReason is not null),
            DateTimeOffset.UtcNow, _currentUser.ActorName,
            computations.Select(ToTransientModel).ToList());
        return Response<ReleaseGateEvaluationModel>.Success(model, correlationId: correlationId);
    }

    // ── engine core (also used by the FU08 port adapter) ────────────────────────

    /// <summary>Evaluates + persists an immutable evaluation and its six results; mutates the entry's extension fields
    /// IN MEMORY (the caller saves the entry). Never deletes prior evaluations.</summary>
    public async Task<(DocumentReleaseGateEvaluation Evaluation, IReadOnlyList<DocumentReleaseGateResult> Results)> EvaluateCoreAsync(
        DocumentMasterRegisterEntry entry, string correlationId, CancellationToken ct)
    {
        var (status, computations) = await ComputeAsync(entry, ct);

        var evaluation = await _evaluations.CreateAsync(new DocumentReleaseGateEvaluation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = entry.Id,
            EvaluationStatus = status,
            EvaluatedAt = DateTimeOffset.UtcNow,
            EvaluatedBy = _currentUser.ActorName,
            GateCount = computations.Count,
            CompletedGateCount = computations.Count(c => c.Result == ReleaseGateResultValue.Yes),
            BlockingCount = computations.Count(c => c.Result == ReleaseGateResultValue.No),
            WarningCount = computations.Count(c => c.WarningReason is not null),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        }, ct);

        var results = new List<DocumentReleaseGateResult>();
        foreach (var c in computations)
        {
            results.Add(await _results.CreateAsync(new DocumentReleaseGateResult
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantContext.TenantId,
                RegisterEntryId = entry.Id,
                EvaluationId = evaluation.Id,
                GateKey = c.Key,
                GateNumber = c.Number,
                GateName = c.Name,
                GateResult = c.Result,
                IsNonWaivable = true,
                ExceptionPermitted = false, // permanently NO (SOP §19.1)
                EvidenceReference = c.EvidenceReference,
                VerifiedByUserId = c.VerifiedByUserId,
                VerifiedByRole = c.VerifiedByRole,
                VerificationDate = c.VerificationDate,
                Source = c.Source,
                BlockingReason = c.BlockingReason,
                WarningReason = c.WarningReason,
                CreatedBy = _currentUser.ActorName
            }, ct));
        }

        entry.LastReleaseGateEvaluationStatus = status.ToString();
        entry.LastReleaseGateEvaluationAt = evaluation.EvaluatedAt;
        entry.LastReleaseGateBlockingCount = evaluation.BlockingCount;
        entry.LastReleaseGateWarningCount = evaluation.WarningCount;

        return (evaluation, results);
    }

    private async Task<(ReleaseGateEvaluationStatus Status, IReadOnlyList<GateComputation> Gates)> ComputeAsync(
        DocumentMasterRegisterEntry entry, CancellationToken ct)
    {
        var gates = new List<GateComputation>
        {
            ComputeGate1(entry),
            await ComputeGate2Async(entry, ct),
            ComputeGate3(entry),
            await ComputeManualGateAsync(entry, ReleaseGateKey.RequiredExecutionMaterialsEffective,
                "Evidence that required forms/templates/registers are effective and available is missing.", ct),
            await ComputeGate5Async(entry, ct),
            await ComputeGate6Async(entry, ct)
        };

        var allYes = gates.All(g => g.Result == ReleaseGateResultValue.Yes);
        var status = allYes ? ReleaseGateEvaluationStatus.Complete : ReleaseGateEvaluationStatus.Blocked;
        return (status, gates);
    }

    private static GateComputation ComputeGate1(DocumentMasterRegisterEntry entry)
    {
        var def = ReleaseGateCatalog.ByKey(ReleaseGateKey.MasterRegisterActive);
        var inactive = entry.RegisterStatus is DocumentRegisterStatus.Archived or DocumentRegisterStatus.Superseded or DocumentRegisterStatus.Retired;
        var missing = new List<string>();
        if (inactive) missing.Add("register entry is not active");
        if (string.IsNullOrWhiteSpace(entry.PermanentUid)) missing.Add("Permanent UID not allocated");
        if (string.IsNullOrWhiteSpace(entry.DocumentCode)) missing.Add("Document Code not allocated");
        if (!entry.IsControlledDocument) missing.Add("entry is not a controlled document");
        if (!DocumentLinkGovernanceGuard.IsGovernedRelationCompatible(entry))
            missing.Add(DocumentLinkGovernanceGuard.BlockingReason);

        return missing.Count == 0
            ? Met(def, ReleaseGateEvidenceSource.Automatic, $"UID {entry.PermanentUid} / Code {entry.DocumentCode} (register {entry.Id})", "System")
            : NotMet(def, $"Gate 1 blocked: {string.Join(", ", missing)}.");
    }

    private static GateComputation ComputeGate3(DocumentMasterRegisterEntry entry)
    {
        var def = ReleaseGateCatalog.ByKey(ReleaseGateKey.MandatoryApprovalEvidence);
        var status = entry.ApprovalEvidenceStatus;
        return string.Equals(status, "Complete", StringComparison.OrdinalIgnoreCase)
            ? Met(def, ReleaseGateEvidenceSource.Computed, $"Approval readiness: {status}", "QADocumentation")
            : NotMet(def, $"Gate 3 blocked: approval evidence status is {(string.IsNullOrWhiteSpace(status) ? "Missing" : status)}.");
    }

    private async Task<GateComputation> ComputeGate6Async(DocumentMasterRegisterEntry entry, CancellationToken ct)
    {
        var def = ReleaseGateCatalog.ByKey(ReleaseGateKey.SupersededCopyWithdrawalMethod);

        // MOD-0029-FU17: when a copy-reconciliation adapter is present, Gate 6 is COMPUTED from controlled-copy
        // withdrawal readiness. A null adapter keeps the FU10 manual Gate 6 evidence behaviour (backward compatible).
        if (_copyPort is not null)
        {
            var decision = await _copyPort.EvaluateGate6Async(entry, ct);
            switch (decision.Outcome)
            {
                case CopyGateOutcome.Pass:
                    return Met(def, ReleaseGateEvidenceSource.Computed, decision.EvidenceReference ?? "Controlled-copy withdrawal readiness satisfied", "LocalQA");
                case CopyGateOutcome.Block:
                    return NotMet(def, decision.Reason ?? "Superseded copies are not withdrawn from point of use.");
                case CopyGateOutcome.FallBackToManual:
                default:
                    break; // fall through to the manual evidence logic below
            }
        }

        return await ComputeManualGateAsync(entry, ReleaseGateKey.SupersededCopyWithdrawalMethod,
            "Evidence of a superseded-copy withdrawal method is missing.", ct);
    }

    private async Task<GateComputation> ComputeGate2Async(DocumentMasterRegisterEntry entry, CancellationToken ct)
    {
        var def = ReleaseGateCatalog.ByKey(ReleaseGateKey.ApprovedRepositoryAvailable);

        // MOD-0029-FU16: when a repository-assessment adapter is present, Gate 2 is COMPUTED from the linked repository
        // assessment (validated DMS / approved interim repository) + its authorised release route. A null adapter keeps
        // the FU10 manual Gate 2 evidence behaviour (backward compatible).
        if (_repositoryPort is not null)
        {
            var decision = await _repositoryPort.EvaluateGate2Async(entry, ct);
            switch (decision.Outcome)
            {
                case RepositoryGateOutcome.Pass:
                    return Met(def, ReleaseGateEvidenceSource.Computed, decision.EvidenceReference ?? "Approved repository assessment", "ITCSVOwner");
                case RepositoryGateOutcome.Block:
                    return NotMet(def, decision.Reason ?? "Approved repository / authorised release route is not established.");
                case RepositoryGateOutcome.FallBackToManual:
                default:
                    break; // fall through to the manual evidence logic below
            }
        }

        return await ComputeManualGateAsync(entry, ReleaseGateKey.ApprovedRepositoryAvailable,
            "Approved repository / authorised release route evidence is missing.", ct);
    }

    private async Task<GateComputation> ComputeGate5Async(DocumentMasterRegisterEntry entry, CancellationToken ct)
    {
        var def = ReleaseGateCatalog.ByKey(ReleaseGateKey.TrainingReadiness);

        // MOD-0029-FU11: when a training-matrix adapter is present, Gate 5 is COMPUTED from role-to-document training
        // requirements + assignment/completion/effectiveness/restriction. A null adapter keeps the FU10 manual/auto
        // behaviour (backward compatible).
        if (_trainingPort is not null)
        {
            var decision = await _trainingPort.EvaluateGate5Async(entry, ct);
            switch (decision.Outcome)
            {
                case TrainingGateOutcome.Pass:
                    return Met(def, ReleaseGateEvidenceSource.Computed, decision.EvidenceReference ?? "Training readiness: Ready", "TrainingCoordinator");
                case TrainingGateOutcome.Block:
                    return NotMet(def, decision.Reason ?? "Training readiness is not met.");
                case TrainingGateOutcome.FallBackToManual:
                default:
                    break; // fall through to the manual/auto logic below
            }
        }

        var required = entry.Criticality == DocumentCriticality.Critical || _options.RequireTrainingEvidenceForNonCritical;
        if (!required)
        {
            return Met(def, ReleaseGateEvidenceSource.Computed, "Training gate not required for this criticality (policy).", "System",
                warning: "Training evidence not required by policy for a non-critical document.");
        }

        return await ComputeManualGateAsync(entry, ReleaseGateKey.TrainingReadiness,
            "Training evidence is required (Critical/policy) but is missing.", ct);
    }

    private async Task<GateComputation> ComputeManualGateAsync(DocumentMasterRegisterEntry entry, ReleaseGateKey key, string blockingReason, CancellationToken ct)
    {
        var def = ReleaseGateCatalog.ByKey(key);
        var ev = await _evidence.GetLatestForGateAsync(entry.Id, key, ct);
        var valid = ev is not null
            && !string.IsNullOrWhiteSpace(ev.EvidenceReference)
            && ev.VerifiedByUserId != Guid.Empty
            && ev.VerificationDate != default;

        return valid
            ? new GateComputation(def.Key, def.Number, def.Name, ReleaseGateResultValue.Yes, ReleaseGateEvidenceSource.ManualEvidence,
                ev!.EvidenceReference, ev.VerifiedByUserId, ev.VerifiedByRole, ev.VerificationDate, null, null)
            : NotMet(def, blockingReason);
    }

    private static GateComputation Met(ReleaseGateCatalog.Definition def, ReleaseGateEvidenceSource source, string evidence, string role, string? warning = null) =>
        new(def.Key, def.Number, def.Name, ReleaseGateResultValue.Yes, source, evidence, null, role, DateTimeOffset.UtcNow, null, warning);

    private static GateComputation NotMet(ReleaseGateCatalog.Definition def, string blockingReason) =>
        new(def.Key, def.Number, def.Name, ReleaseGateResultValue.No, ReleaseGateEvidenceSource.Computed, null, null, null, null, blockingReason, null);

    private static ReleaseGateResultModel ToTransientModel(GateComputation c) => new(
        c.Number, c.Key.ToString(), c.Name, c.Result.ToString(), true, false,
        c.EvidenceReference, c.VerifiedByUserId, c.VerifiedByRole, c.VerificationDate, c.Source.ToString(), c.BlockingReason, c.WarningReason);

    private static Response<ReleaseGateEvaluationModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<ReleaseGateEvaluationModel>.Fail(error, status, reason, correlationId);

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record GateComputation(
        ReleaseGateKey Key, int Number, string Name, ReleaseGateResultValue Result, ReleaseGateEvidenceSource Source,
        string? EvidenceReference, Guid? VerifiedByUserId, string? VerifiedByRole, DateTimeOffset? VerificationDate,
        string? BlockingReason, string? WarningReason);
}
