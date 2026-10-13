using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.ESignature.Services;

/// <summary>Why a signature does not bind an approval (logged / tested; the caller only ever sees ESIGN_SIGNATURE_MISMATCH).</summary>
public enum ESignatureGateFailure
{
    None,
    NotFound,
    WrongSigner,
    WrongMeaning,
    WrongTask,
    WrongDecision,
    WrongSubject,
    SignedBeforeTask,
    NotValid,
    AlreadyBound
}

public sealed record ESignatureGateResult(ESignatureGateFailure Failure, string? VerificationStatus = null)
{
    public bool Binds => Failure == ESignatureGateFailure.None;
}

/// <summary>
/// MOD-0022 E4 — the in-process check MOD-0023 runs before it approves a step whose template says
/// <c>requirements.signatureRequired</c> (ESIGN-BUNDLE <c>x-workflow-addendum</c>). Used by
/// <c>WorkflowTaskTransitionSupport</c> for BOTH the API and the WorkCenter path (one handler).
/// </summary>
public interface IESignatureGate
{
    Task<ESignatureGateResult> ValidateForApprovalAsync(
        string signatureId, ApprovalTask task, WorkflowInstance instance, string actorId, CancellationToken ct);
}

/// <summary>
/// The contract rule, in this order — every miss is the same 409 <c>ESIGN_SIGNATURE_MISMATCH</c> to the caller:
/// <list type="number">
/// <item>(a) the record exists in the caller's tenant (another tenant's id is "not found");</item>
/// <item>(b) <c>signer.userId == actorId</c> — the authenticated approver (WP-0023-FX binds the actor), never a body value;</item>
/// <item>(d) <c>meaningCode == step.signatureMeaning</c>;</item>
/// <item>(c) <c>context.workflowTaskId == taskId</c>, <c>context.decision == approve</c>, <c>context.workflowInstanceId ==
/// instance</c>, and the subject is this task (<c>workflow / workflow-task / {taskId}</c>, the ASSUMPTION-0022-05 fallback) or
/// the instance's own business object (<c>subjectType == objectType</c>, <c>subjectId == objectId</c> — ASSUMPTION-0022-E4-01);</item>
/// <item><c>signedAtUtc ≥ task.createdAt</c> (no signature made before the task existed);</item>
/// <item>(e) verification <c>valid</c>: not invalidated, subject hash unchanged, image and digest intact;</item>
/// <item>(f) single use: no transition log already carries this signature id.</item>
/// </list>
/// </summary>
public sealed class ESignatureGate : IESignatureGate
{
    public const string ApproveDecision = "approve";

    private readonly IESignatureRecordRepository _records;
    private readonly IESignatureInvalidationRepository _invalidations;
    private readonly IESignatureVerifier _verifier;
    private readonly IWorkflowTransitionLogRepository _logs;
    private readonly ITenantContext _tenant;

    public ESignatureGate(
        IESignatureRecordRepository records,
        IESignatureInvalidationRepository invalidations,
        IESignatureVerifier verifier,
        IWorkflowTransitionLogRepository logs,
        ITenantContext tenant)
    {
        _records = records;
        _invalidations = invalidations;
        _verifier = verifier;
        _logs = logs;
        _tenant = tenant;
    }

    public async Task<ESignatureGateResult> ValidateForApprovalAsync(
        string signatureId, ApprovalTask task, WorkflowInstance instance, string actorId, CancellationToken ct)
    {
        if (!Guid.TryParse(signatureId, out var id))
        {
            return new(ESignatureGateFailure.NotFound);
        }

        var record = await _records.GetAsync(_tenant.TenantId, id, ct);
        if (record is null || record.TenantId != task.TenantId)
        {
            return new(ESignatureGateFailure.NotFound);
        }

        if (!SameUser(record.SignerUserId, actorId))
        {
            return new(ESignatureGateFailure.WrongSigner);
        }

        if (string.IsNullOrWhiteSpace(task.SignatureMeaning) || !string.Equals(record.MeaningCode, task.SignatureMeaning, StringComparison.Ordinal))
        {
            return new(ESignatureGateFailure.WrongMeaning);
        }

        if (!Context(record, "workflowTaskId", task.Id) || !Context(record, "workflowInstanceId", instance.Id))
        {
            return new(ESignatureGateFailure.WrongTask);
        }

        if (!record.Context.TryGetValue("decision", out var decision) || !string.Equals(decision, ApproveDecision, StringComparison.Ordinal))
        {
            return new(ESignatureGateFailure.WrongDecision);
        }

        if (!IsThisSubject(record, task, instance))
        {
            return new(ESignatureGateFailure.WrongSubject);
        }

        // The record keeps milliseconds (E2 note); compare at that precision.
        var created = task.CreatedAt.UtcDateTime;
        created = created.AddTicks(-(created.Ticks % TimeSpan.TicksPerMillisecond));
        if (DateTime.SpecifyKind(record.SignedAtUtc, DateTimeKind.Utc) < created)
        {
            return new(ESignatureGateFailure.SignedBeforeTask);
        }

        var verification = await _verifier.VerifyAsync(record, await _invalidations.GetBySignatureAsync(record.TenantId, record.Id, ct), ct);
        if (!string.Equals(verification.Status, ESignatureWire.Name(ESignatureVerificationStatus.Valid), StringComparison.Ordinal))
        {
            return new(ESignatureGateFailure.NotValid, verification.Status);
        }

        if (await _logs.IsSignatureBoundAsync(record.Id.ToString("D"), ct))
        {
            return new(ESignatureGateFailure.AlreadyBound);
        }

        return new(ESignatureGateFailure.None, verification.Status);
    }

    private static bool IsThisSubject(ESignatureRecord record, ApprovalTask task, WorkflowInstance instance)
        => (string.Equals(record.Module, WorkflowTaskSignableSubject.Module, StringComparison.Ordinal)
            && string.Equals(record.SubjectType, WorkflowTaskSignableSubject.SubjectType, StringComparison.Ordinal)
            && SameId(record.SubjectId, task.Id))
           || (string.Equals(record.SubjectType, instance.ObjectType, StringComparison.Ordinal)
               && string.Equals(record.SubjectId, instance.ObjectId, StringComparison.Ordinal));

    private static bool Context(ESignatureRecord record, string key, Guid expected)
        => record.Context.TryGetValue(key, out var value) && SameId(value, expected);

    private static bool SameId(string? value, Guid expected) => Guid.TryParse(value, out var parsed) && parsed == expected;

    private static bool SameUser(string? signer, string actor)
        => Guid.TryParse(signer, out var s) && Guid.TryParse(actor, out var a)
            ? s == a
            : string.Equals(signer?.Trim(), actor.Trim(), StringComparison.Ordinal);
}

/// <summary>The fallback subject of a workflow task (ASSUMPTION-0022-05): module <c>workflow</c>, type <c>workflow-task</c>.</summary>
public static class WorkflowTaskSignableSubject
{
    public const string Module = "workflow";
    public const string SubjectType = "workflow-task";
}
