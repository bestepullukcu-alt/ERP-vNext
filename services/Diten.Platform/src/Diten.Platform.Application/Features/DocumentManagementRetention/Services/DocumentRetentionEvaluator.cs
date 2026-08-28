using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementRetention.Services;

/// <summary>
/// MOD-0029-FU15 — computes the retention verdict for one regulated record (GMG-QMS-SOP-0001 §22) and persists it
/// as a <see cref="DocumentRetentionSubject"/> snapshot.
///
/// It answers only four questions: how long must this be kept, when does retention elapse, is a legal hold in
/// force, and is a disposition REQUEST therefore permissible. It NEVER deletes, purges, archives, hides or
/// otherwise mutates the subject record — evaluation is a read-only observation of the governed aggregate.
///
/// FAIL-CLOSED PRECEDENCE (most protective first):
/// 1. An active legal hold blocks everything, regardless of elapsed retention.
/// 2. The identifier allocation ledger is never disposition eligible — UIDs and codes must never be reused.
/// 3. No matching active policy → MissingPolicy → not eligible.
/// 4. A permanent-retention policy → never eligible.
/// 5. No resolvable trigger date → MissingTriggerDate → not eligible (the clock cannot start).
/// 6. RetainWhileEffective and the document is still Effective → not eligible.
/// 7. Otherwise eligible only once the due date has passed.
///
/// The applicable retention period is the LONGEST across all matching active policies (SOP §22 longest applicable
/// requirement), and within a policy the longest of its minimum / post-retirement / post-supersession periods.
/// </summary>
public sealed class DocumentRetentionEvaluator
{
    private readonly IDocumentRetentionPolicyRepository _policies;
    private readonly IDocumentRetentionSubjectRepository _subjects;
    private readonly DocumentRetentionTriggerDateResolver _triggerResolver;
    private readonly DocumentLegalHoldEvaluator _holdEvaluator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentRetentionEvaluator(
        IDocumentRetentionPolicyRepository policies,
        IDocumentRetentionSubjectRepository subjects,
        DocumentRetentionTriggerDateResolver triggerResolver,
        DocumentLegalHoldEvaluator holdEvaluator,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _policies = policies;
        _subjects = subjects;
        _triggerResolver = triggerResolver;
        _holdEvaluator = holdEvaluator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<RetentionSubjectModel>> EvaluateAsync(EvaluateRetentionInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var subjectType = RetentionWire.ParseSubjectType(input.SubjectType);
        if (subjectType is null || input.SubjectId == Guid.Empty)
        {
            return Response<RetentionSubjectModel>.Fail(
                "A valid subject type and subject id are required.", 400, RetentionReasonCodes.ValidationFailed, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var trigger = await _triggerResolver.ResolveAsync(input, subjectType.Value, ct);
        var policy = await SelectLongestApplicableAsync(subjectType.Value, trigger.RetentionClass, ct);
        var holds = await _holdEvaluator.GetBlockingHoldsAsync(
            subjectType.Value, input.SubjectId, input.RegisterEntryId, input.ControlledDocumentId, now, ct);

        var snapshot = await _subjects.GetBySubjectAsync(subjectType.Value, input.SubjectId, ct);
        var isNew = snapshot is null;
        snapshot ??= new DocumentRetentionSubject
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubjectType = subjectType.Value,
            SubjectId = input.SubjectId,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        snapshot.RegisterEntryId = input.RegisterEntryId ?? snapshot.RegisterEntryId;
        snapshot.ControlledDocumentId = input.ControlledDocumentId ?? snapshot.ControlledDocumentId;
        snapshot.RetentionClass = trigger.RetentionClass;
        snapshot.RetentionTriggerDate = trigger.TriggerDate;
        snapshot.PolicyId = policy?.Id;
        snapshot.PolicyKey = policy?.PolicyKey;
        snapshot.IsPermanentRetention = policy?.IsPermanentRetention ?? false;
        snapshot.ActiveLegalHoldIds = holds.Select(h => h.Id).ToList();
        snapshot.IsBlockedByLegalHold = holds.Count > 0;
        snapshot.LastEvaluatedAt = now;
        snapshot.LastEvaluatedBy = _currentUser.ActorName;

        // Retention due date is informational whenever it can be computed, even if something else blocks.
        snapshot.RetentionDueDate = policy is not null && trigger.TriggerDate is { } start
            ? start.AddYears(policy.EffectiveRetentionYears())
            : null;

        ApplyVerdict(snapshot, subjectType.Value, policy, trigger, holds, now);

        if (isNew)
        {
            await _subjects.CreateAsync(snapshot, ct);
        }
        else
        {
            snapshot.UpdatedAt = now;
            snapshot.UpdatedBy = _currentUser.ActorName;
            await _subjects.UpdateAsync(snapshot, ct);
        }

        return Response<RetentionSubjectModel>.Success(RetentionWire.ToSubject(snapshot), correlationId: correlationId);
    }

    public async Task<Response<RetentionSubjectModel>> GetSubjectAsync(
        string subjectTypeRaw, Guid subjectId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var subjectType = RetentionWire.ParseSubjectType(subjectTypeRaw);
        if (subjectType is null)
        {
            return Response<RetentionSubjectModel>.Fail(
                "A valid subject type is required.", 400, RetentionReasonCodes.ValidationFailed, correlationId);
        }

        var snapshot = await _subjects.GetBySubjectAsync(subjectType.Value, subjectId, ct);
        return snapshot is null
            ? Response<RetentionSubjectModel>.Fail(
                "Retention subject not found. Evaluate the subject first.", 404, RetentionReasonCodes.SubjectNotFound, correlationId)
            : Response<RetentionSubjectModel>.Success(RetentionWire.ToSubject(snapshot), correlationId: correlationId);
    }

    /// <summary>Subjects past retention with no active hold. These are disposition REQUEST candidates only.</summary>
    public async Task<Response<IReadOnlyList<RetentionSubjectModel>>> GetEligibleAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _subjects.GetEligibleAsync(ct);
        return Response<IReadOnlyList<RetentionSubjectModel>>.Success(
            rows.Select(RetentionWire.ToSubject).ToList(), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// SOP §22 longest applicable requirement: among all active policies matching the subject type (and, when the
    /// policy narrows by retention class, that class), the one with the longest effective period wins. A permanent
    /// policy always wins outright.
    /// </summary>
    private async Task<DocumentRetentionPolicy?> SelectLongestApplicableAsync(
        RetentionSubjectType subjectType, string? retentionClass, CancellationToken ct)
    {
        var candidates = (await _policies.GetActiveBySubjectTypeAsync(subjectType, ct))
            .Where(p => string.IsNullOrWhiteSpace(p.RetentionClass)
                        || string.Equals(p.RetentionClass, retentionClass, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates.FirstOrDefault(p => p.IsPermanentRetention)
               ?? candidates.OrderByDescending(p => p.EffectiveRetentionYears()).First();
    }

    private static void ApplyVerdict(
        DocumentRetentionSubject snapshot,
        RetentionSubjectType subjectType,
        DocumentRetentionPolicy? policy,
        DocumentRetentionTriggerDateResolver.Result trigger,
        IReadOnlyList<DocumentLegalHold> holds,
        DateTimeOffset now)
    {
        snapshot.IsDispositionEligible = false;
        snapshot.DispositionEligibleAt = null;

        // 1. A litigation hold stops everything (SOP §22).
        if (holds.Count > 0)
        {
            snapshot.EvaluationStatus = RetentionEvaluationStatus.BlockedByHold;
            snapshot.EvaluationNote = $"Blocked by {holds.Count} active legal hold(s): {string.Join(", ", holds.Select(h => h.HoldKey))}.";
            return;
        }

        // 2. The identifier allocation ledger is permanent by construction — UIDs/codes are never reused.
        if (subjectType == RetentionSubjectType.IdentifierAllocationLedger)
        {
            snapshot.IsPermanentRetention = true;
            snapshot.EvaluationStatus = RetentionEvaluationStatus.Current;
            snapshot.EvaluationNote = "Identifier allocation ledger is a permanent record; never disposition eligible.";
            return;
        }

        // 3. No policy → fail closed.
        if (policy is null)
        {
            snapshot.EvaluationStatus = RetentionEvaluationStatus.MissingPolicy;
            snapshot.EvaluationNote = "No active retention policy matches this subject; disposition is not permitted.";
            return;
        }

        // 4. Permanent retention.
        if (policy.IsPermanentRetention)
        {
            snapshot.EvaluationStatus = RetentionEvaluationStatus.Current;
            snapshot.EvaluationNote = $"Policy '{policy.PolicyKey}' retains this record permanently.";
            return;
        }

        // 5. The clock cannot start.
        if (trigger.TriggerDate is null)
        {
            snapshot.EvaluationStatus = RetentionEvaluationStatus.MissingTriggerDate;
            snapshot.EvaluationNote = $"Retention trigger '{policy.RetentionTrigger}' has no resolvable date; the retention clock cannot start.";
            return;
        }

        // 6. An effective controlled document is retained regardless of elapsed time (SOP §22).
        if (policy.RetainWhileEffective && trigger.IsStillEffective)
        {
            snapshot.EvaluationStatus = RetentionEvaluationStatus.Current;
            snapshot.EvaluationNote = "Document is still Effective; retained while effective regardless of elapsed period.";
            return;
        }

        var due = trigger.TriggerDate.Value.AddYears(policy.EffectiveRetentionYears());
        snapshot.RetentionDueDate = due;

        if (now >= due)
        {
            snapshot.IsDispositionEligible = true;
            snapshot.DispositionEligibleAt = due;
            snapshot.EvaluationStatus = RetentionEvaluationStatus.Eligible;
            snapshot.EvaluationNote = $"Retention of {policy.EffectiveRetentionYears()} year(s) elapsed on {due:yyyy-MM-dd}; a disposition request may be raised.";
            return;
        }

        snapshot.EvaluationStatus = RetentionEvaluationStatus.Current;
        snapshot.EvaluationNote = $"Retained until {due:yyyy-MM-dd} under policy '{policy.PolicyKey}'.";
    }
}
