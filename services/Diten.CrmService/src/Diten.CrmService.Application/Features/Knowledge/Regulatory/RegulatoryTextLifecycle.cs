using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Chain;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Knowledge.Regulatory;

/// <summary>What the caller can do with a text (computed per read; permissions themselves are the API's job).</summary>
public sealed record RegulatoryTextAbilities(bool CanEdit, bool CanSubmit, bool CanDecide);

/// <summary>
/// WP-KP-5a — the shared lifecycle of a regulatory text kind: key rules (one open, one active), the Regulatory round
/// (submit / withdraw / decide on the SAME MOD-0023 task, K1), new version, archive and the reads (reconciled). The two
/// kinds' handlers own only their content fields.
/// </summary>
public sealed class RegulatoryTextLifecycle<T> where T : RegulatoryText
{
    private readonly RegulatoryTextKind _kind;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IRegulatoryTextRepository<T> _repository;
    private readonly IClaimWorkflowClient _workflow;
    private readonly IWorkflowDecisionClient _decisions;
    private readonly RegulatoryTextOutcomeApplier _applier;
    private readonly RegulatoryTextReviewReconciler? _reconciler;
    private readonly IRegulatoryTextReviewSettings? _settings;

    public RegulatoryTextLifecycle(RegulatoryTextKind kind, ITenantContext tenant, IActorContext actor,
        IRegulatoryTextRepository<T> repository, IClaimWorkflowClient workflow, IWorkflowDecisionClient decisions,
        RegulatoryTextOutcomeApplier applier, RegulatoryTextReviewReconciler? reconciler = null,
        IRegulatoryTextReviewSettings? settings = null)
    {
        _kind = kind;
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _workflow = workflow;
        _decisions = decisions;
        _applier = applier;
        _reconciler = reconciler;
        _settings = settings;
    }

    public RegulatoryTextKind Kind => _kind;

    public string? Actor => _actor.ActorName;

    public Guid? TenantId => _tenant.TenantId;

    // ---------------------------------------------------------------- reads

    /// <summary>All texts of the tenant after reconcile-on-read.</summary>
    public async Task<IReadOnlyList<T>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        var rows = await _repository.ListAsync(tenantId, ct);
        if (_reconciler is not null && rows.Any(r => r.OpenRound() is not null)
            && await _reconciler.ReconcileAsync(tenantId, _kind, rows, ct) > 0)
        {
            rows = await _repository.ListAsync(tenantId, ct);
        }

        return rows;
    }

    /// <summary>The caller's abilities on <paramref name="texts"/> (one MOD-0023 task read, only when a text is in review).</summary>
    public async Task<Func<T, RegulatoryTextAbilities>> AbilitiesAsync(IEnumerable<T> texts, CancellationToken ct)
    {
        var mine = new HashSet<Guid>();
        if (texts.Any(t => t.OpenRound() is not null) && await _decisions.GetMyTasksAsync(ct) is { } tasks)
        {
            mine = tasks.Where(t => t.IsOpen).Select(t => t.WorkflowInstanceId).ToHashSet();
        }

        var actor = _actor.ActorName;
        return t => new RegulatoryTextAbilities(
            CanEdit: t.IsDraft() && !t.IsArchived(),
            CanSubmit: t.IsDraft() && !t.IsArchived(),
            CanDecide: t.OpenRound() is { } round && mine.Contains(round.WorkflowInstanceId)
                       && !RegulatoryTextRules.IsOwnVersion(t, actor));
    }

    // ---------------------------------------------------------------- key rules

    /// <summary>409 when the key already has an open (draft / in-review) version.</summary>
    public Response<TOut>? CheckNoOpenVersion<TOut>(IEnumerable<T> sameKey)
        => sameKey.Any(r => r.IsOpenVersion())
            ? RegulatoryTextRules.Fail<TOut>(_kind.OpenDraftError,
                "This key already has an open draft or a version in review; finish or withdraw it first.", 409)
            : null;

    /// <summary>The key's code (shared by its versions) or a new <c>{PREFIX}-{CC}-{seq}</c>.</summary>
    public string CodeFor(IReadOnlyList<T> all, IReadOnlyList<T> sameKey, string countryCode)
    {
        if (sameKey.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Code)) is { } existing)
        {
            return existing.Code;
        }

        var prefix = $"{_kind.CodePrefix}-{countryCode}-";
        var next = all.Select(r => r.Code)
            .Where(c => c is not null && c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(c[prefix.Length..], out _))
            .Select(c => int.Parse(c[prefix.Length..]))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"{prefix}{next:D4}";
    }

    public static int NextVersionNumber(IEnumerable<T> sameKey) => sameKey.Select(r => r.VersionNumber).DefaultIfEmpty(0).Max() + 1;

    /// <summary>Country + language through the KP-1 validator (BRD COUNTRY_CODES + country-content-languages).</summary>
    public static Task<ChainContextValidation.Outcome> ValidateContextAsync(
        IReferenceDataCatalogReader? catalog, string? country, string? language, CancellationToken ct)
        => ChainContextValidation.ValidateAsync(catalog, country, language, ct);

    public Response<TOut>? CheckEditable<TOut>(T text, int? expectedVersion)
    {
        if (!text.IsDraft() || text.IsArchived())
        {
            return RegulatoryTextRules.Fail<TOut>(RegulatoryTextErrors.NotEditable,
                $"Only a draft can be edited (status: {text.Status}).", 409);
        }

        return expectedVersion is { } v && v != text.Version
            ? Response<TOut>.Fail("The record was modified by another writer; reload and retry.", 409)
            : null;
    }

    // ---------------------------------------------------------------- lifecycle

    public async Task<(T? Text, Response<TOut>? Error)> LoadAsync<TOut>(Guid id, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return (null, Response<TOut>.Fail("Tenant context is required.", 400));
        }

        var text = await _repository.GetByIdAsync(tenantId, id, ct);
        if (text is not null && text.OpenRound() is not null && _reconciler is not null
            && await _reconciler.ReconcileAsync(tenantId, _kind, new[] { text }, ct) > 0)
        {
            text = await _repository.GetByIdAsync(tenantId, id, ct);
        }

        return text is null ? (null, Response<TOut>.Fail("Not found.", 404)) : (text, null);
    }

    /// <summary>Starts the country's Regulatory round (caller's token) and moves the draft to <c>in-review</c>.</summary>
    public async Task<(T? Text, Response<TOut>? Error)> SubmitAsync<TOut>(Guid id, Func<T, string> subtitle, CancellationToken ct)
    {
        var (text, error) = await LoadAsync<TOut>(id, ct);
        if (error is not null)
        {
            return (null, error);
        }

        if (!text!.IsDraft() || text.IsArchived())
        {
            return (null, RegulatoryTextRules.Fail<TOut>(RegulatoryTextErrors.NotEditable,
                $"Only a draft can be sent for approval (status: {text.Status}).", 409));
        }

        var roundNo = text.ReviewRounds.Select(r => r.RoundNo).DefaultIfEmpty(0).Max() + 1;
        var templateCode = string.Format(
            _settings?.TemplateCodeFormat ?? RegulatoryTextReviewDefaults.TemplateCodeFormat, text.CountryCode);
        var start = await _workflow.StartAsync(new ClaimWorkflowStartRequest(
            templateCode,
            _kind.ObjectType,
            text.Id.ToString("D"),
            _kind.ObjectRef(text.Id),
            _kind.IdempotencyKey(text.Id, roundNo),
            new ClaimWorkflowDisplayContext(
                ClaimReviewRules.Truncate($"{_kind.TitleNoun} · {text.Code} v{text.VersionNumber}", 200),
                ClaimReviewRules.Truncate(subtitle(text), 300),
                "crm",
                _kind.ReviewLink(text.Id),
                [
                    ClaimReviewRules.Truncate(text.CountryCode, 32),
                    ClaimReviewRules.Truncate(text.LanguageCode, 32),
                    ClaimReviewRules.Truncate($"v{text.VersionNumber}", 32)
                ])), ct);
        if (start.Outcome != ClaimWorkflowCallOutcome.Ok || start.WorkflowInstanceId is not { } instanceId)
        {
            return (null, RegulatoryTextRules.FromStart<TOut>(start));
        }

        var now = DateTimeOffset.UtcNow;
        var round = new ClaimReviewRound
        {
            WorkflowInstanceId = instanceId, SubmittedAt = now, SubmittedBy = _actor.ActorName, RoundNo = roundNo,
            TemplateCode = templateCode
        };

        // One retry on an optimistic conflict with a concurrent edit (MOD-0023 already holds the round).
        for (var attempt = 0; attempt < 2; attempt++)
        {
            text.ReviewRounds.Add(round);
            text.Status = RegulatoryTextStatuses.InReview;
            text.UpdatedAt = now;
            text.UpdatedBy = _actor.ActorName;
            if (await _repository.ReplaceAsync(text, text.Version, ct))
            {
                return (text, null);
            }

            text = await _repository.GetByIdAsync(_tenant.TenantId!.Value, id, ct);
            if (text is null || !text.IsDraft())
            {
                break;
            }
        }

        return (null, Response<TOut>.Fail("The record was modified by another writer; reload and retry.", 409));
    }

    /// <summary>Cancels the open round (only its submitter); the text returns to draft through the applier.</summary>
    public async Task<(T? Text, Response<TOut>? Error)> WithdrawAsync<TOut>(Guid id, CancellationToken ct)
    {
        var (text, error) = await LoadAsync<TOut>(id, ct);
        if (error is not null)
        {
            return (null, error);
        }

        if (text!.OpenRound() is not { } round)
        {
            return (null, RegulatoryTextRules.Fail<TOut>(ClaimErrorCodes.NoOpenReview, "There is no open approval to withdraw.", 409));
        }

        var actor = _actor.ActorName;
        if (!KnowledgePathReviewRules.SamePerson(actor, round.SubmittedBy))
        {
            return (null, RegulatoryTextRules.Fail<TOut>(ClaimErrorCodes.ApprovalForbidden,
                "Only the person who sent the text for approval can withdraw it.", 403));
        }

        var tasks = await _workflow.GetTasksAsync([round.WorkflowInstanceId], ct);
        if (tasks is null)
        {
            return (null, RegulatoryTextRules.Unavailable<TOut>());
        }

        var open = tasks.Where(t => t.WorkflowInstanceId == round.WorkflowInstanceId && t.IsOpen)
            .OrderByDescending(t => t.DueAt ?? DateTimeOffset.MinValue).FirstOrDefault();
        if (open is null)
        {
            return (null, RegulatoryTextRules.Fail<TOut>(ClaimErrorCodes.WithdrawNotPossible,
                "The workflow has no waiting step to cancel (it may have just finished).", 409));
        }

        var cancel = await _workflow.CancelTaskAsync(open.TaskId, actor!, _kind.WithdrawReason,
            $"{_kind.IdempotencyKey(text.Id, round.RoundNo)}:withdraw", ct);
        switch (cancel)
        {
            case ClaimWorkflowCallOutcome.Ok:
                break;
            case ClaimWorkflowCallOutcome.Forbidden:
                return (null, RegulatoryTextRules.Fail<TOut>(ClaimErrorCodes.ApprovalForbidden,
                    "You are not allowed to cancel this approval.", 403));
            case ClaimWorkflowCallOutcome.Unavailable:
                return (null, RegulatoryTextRules.Unavailable<TOut>());
            default:
                return (null, RegulatoryTextRules.Fail<TOut>(ClaimErrorCodes.WithdrawNotPossible,
                    "The approval workflow refused to cancel the waiting step.", 409));
        }

        await _applier.ApplyAsync(text.TenantId, _kind.ObjectType, text.Id, round.WorkflowInstanceId,
            ClaimReviewOutcomes.Cancelled, actor, _kind.WithdrawReason, DateTimeOffset.UtcNow, ct);
        return (await _repository.GetByIdAsync(text.TenantId, text.Id, ct) ?? text, null);
    }

    /// <summary>
    /// A Regulatory decision on the SAME MOD-0023 task the Work Center shows (K1). A rejection needs a comment (400
    /// <c>rejection_comment_required</c>); the author / submitter never decides (403 <c>self_decision_forbidden</c>). The
    /// decision (with its comment) is recorded on the text; the state change itself arrives as the round's outcome.
    /// </summary>
    public async Task<(T? Text, Response<TOut>? Error)> DecideAsync<TOut>(
        Guid id, string? decision, string? comment, CancellationToken ct)
    {
        var normalized = decision?.Trim().ToLowerInvariant();
        if (normalized is not (RegulatoryTextDecisionOutcomes.Approve or RegulatoryTextDecisionOutcomes.Reject))
        {
            return (null, RegulatoryTextRules.Fail<TOut>(RegulatoryTextErrors.DecisionInvalid,
                "Decision must be 'approve' or 'reject'.", 400));
        }

        var text = RegulatoryTextRules.Clean(comment);
        if (normalized == RegulatoryTextDecisionOutcomes.Reject && text is null)
        {
            return (null, RegulatoryTextRules.Fail<TOut>(RegulatoryTextErrors.RejectionCommentRequired,
                "A rejection needs a comment (the reason the author will act on).", 400));
        }

        if (text is { Length: > RegulatoryTextLimits.Comment })
        {
            return (null, RegulatoryTextRules.Fail<TOut>(RegulatoryTextErrors.TextTooLong,
                $"The comment cannot exceed {RegulatoryTextLimits.Comment} characters.", 400));
        }

        var (record, error) = await LoadAsync<TOut>(id, ct);
        if (error is not null)
        {
            return (null, error);
        }

        if (record!.OpenRound() is not { } round)
        {
            return (null, RegulatoryTextRules.Fail<TOut>(ClaimErrorCodes.NoOpenReview, "This text has no open approval.", 409));
        }

        var actor = _actor.ActorName;
        if (string.IsNullOrWhiteSpace(actor))
        {
            return (null, RegulatoryTextRules.Fail<TOut>(ClaimErrorCodes.ApprovalForbidden,
                "The caller's identity is required to decide.", 403));
        }

        if (RegulatoryTextRules.IsOwnVersion(record, actor))
        {
            return (null, RegulatoryTextRules.Fail<TOut>(RegulatoryTextErrors.SelfDecisionForbidden,
                "The author or submitter of a text cannot decide on it.", 403));
        }

        var mine = await _decisions.GetMyTasksAsync(ct);
        if (mine is null)
        {
            return (null, RegulatoryTextRules.Unavailable<TOut>());
        }

        var task = mine.FirstOrDefault(t => t.WorkflowInstanceId == round.WorkflowInstanceId && t.IsOpen);
        if (task is null)
        {
            // Not a candidate: call the instance's open task anyway so MOD-0023's own 403 is returned as is (KP-2).
            var all = await _workflow.GetTasksAsync([round.WorkflowInstanceId], ct);
            if (all is null)
            {
                return (null, RegulatoryTextRules.Unavailable<TOut>());
            }

            task = all.FirstOrDefault(t => t.WorkflowInstanceId == round.WorkflowInstanceId && t.IsOpen);
            if (task is null)
            {
                return (null, RegulatoryTextRules.Fail<TOut>(ClaimErrorCodes.NoOpenReview,
                    "The approval has no waiting step (it may have just finished).", 409));
            }
        }

        var approve = normalized == RegulatoryTextDecisionOutcomes.Approve;
        var result = await _decisions.DecideTaskAsync(task.TaskId, approve, actor,
            approve ? _kind.ApproveReason : _kind.RejectReason,
            $"{_kind.IdempotencyKey(record.Id, round.RoundNo)}:{task.TaskId:D}:{normalized}", text, ct);
        switch (result.Outcome)
        {
            case ClaimWorkflowCallOutcome.Ok:
                break;
            case ClaimWorkflowCallOutcome.Forbidden:
                return (null, RegulatoryTextRules.Fail<TOut>(ClaimErrorCodes.ApprovalForbidden,
                    result.Detail ?? "You are not a reviewer of this step.", 403));
            case ClaimWorkflowCallOutcome.NotFound:
                return (null, RegulatoryTextRules.Fail<TOut>(ClaimErrorCodes.NoOpenReview, "The approval step is no longer waiting.", 409));
            case ClaimWorkflowCallOutcome.Rejected:
                return (null, RegulatoryTextRules.Fail<TOut>(ClaimErrorCodes.WorkflowRequestRejected,
                    $"The approval workflow refused the decision: {result.Detail}", 400));
            default:
                return (null, RegulatoryTextRules.Unavailable<TOut>());
        }

        // Record the decision with its comment (the outcome event may already have recorded it without one).
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var current = await _repository.GetByIdAsync(record.TenantId, record.Id, ct);
            if (current is null)
            {
                break;
            }

            var existing = current.Decisions.FirstOrDefault(d => d.RoundNo == round.RoundNo);
            if (existing is null)
            {
                current.Decisions.Add(new RegulatoryTextDecision
                {
                    RoundNo = round.RoundNo, By = actor, Outcome = normalized, Comment = text, At = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existing.By ??= actor;
                existing.Comment ??= text;
            }

            if (await _repository.ReplaceAsync(current, current.Version, ct))
            {
                return (current, null);
            }
        }

        return (await _repository.GetByIdAsync(record.TenantId, record.Id, ct) ?? record, null);
    }

    /// <summary>A new draft cloned from the active (or a superseded) version: <c>VersionNumber + 1</c>, empty review.</summary>
    public async Task<(T? Source, IReadOnlyList<T> SameKey, int NextVersion, Response<TOut>? Error)> PrepareNewVersionAsync<TOut>(
        Guid id, CancellationToken ct)
    {
        var (source, error) = await LoadAsync<TOut>(id, ct);
        if (error is not null)
        {
            return (null, Array.Empty<T>(), 0, error);
        }

        if (source!.IsArchived() || !(source.IsActive() || source.IsSuperseded()))
        {
            return (null, Array.Empty<T>(), 0, RegulatoryTextRules.Fail<TOut>(RegulatoryTextErrors.NewVersionSourceInvalid,
                "A new version is cloned from the active or a superseded version only.", 409));
        }

        var sameKey = (await _repository.ListAsync(source.TenantId, ct)).Where(r => r.Key() == source.Key()).ToList();
        if (CheckNoOpenVersion<TOut>(sameKey) is { } open)
        {
            return (null, sameKey, 0, open);
        }

        return (source, sameKey, NextVersionNumber(sameKey), null);
    }

    /// <summary>Stamps a new draft (clone or create) and inserts it.</summary>
    public async Task<T> InsertDraftAsync(T draft, Guid tenantId, string code, int versionNumber, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        draft.Id = Guid.NewGuid();
        draft.TenantId = tenantId;
        draft.Code = code;
        draft.VersionNumber = versionNumber;
        draft.Status = RegulatoryTextStatuses.Draft;
        draft.CreatedAt = now;
        draft.CreatedBy = _actor.ActorName;
        await _repository.InsertAsync(draft, ct);
        return draft;
    }

    /// <summary>Soft archive (never while in review). An archived active text no longer resolves.</summary>
    public async Task<(T? Text, Response<TOut>? Error)> ArchiveAsync<TOut>(Guid id, CancellationToken ct)
    {
        var (text, error) = await LoadAsync<TOut>(id, ct);
        if (error is not null)
        {
            return (null, error);
        }

        if (text!.IsArchived())
        {
            return (text, null);
        }

        if (text.IsInReview())
        {
            return (null, RegulatoryTextRules.Fail<TOut>(RegulatoryTextErrors.NotEditable,
                "A text in review cannot be archived; withdraw it first.", 409));
        }

        var now = DateTimeOffset.UtcNow;
        text.Status = RegulatoryTextStatuses.Archived;
        text.ArchivedAt = now;
        text.ArchivedBy = _actor.ActorName;
        text.UpdatedAt = now;
        text.UpdatedBy = _actor.ActorName;
        return await _repository.ReplaceAsync(text, text.Version, ct)
            ? (text, null)
            : (null, Response<TOut>.Fail("The record was modified by another writer; reload and retry.", 409));
    }

    public async Task<(T? Text, Response<TOut>? Error)> SaveAsync<TOut>(T text, CancellationToken ct)
    {
        text.UpdatedAt = DateTimeOffset.UtcNow;
        text.UpdatedBy = _actor.ActorName;
        return await _repository.ReplaceAsync(text, text.Version, ct)
            ? (text, null)
            : (null, Response<TOut>.Fail("The record was modified by another writer; reload and retry.", 409));
    }
}
